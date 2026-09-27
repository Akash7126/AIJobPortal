using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

/// <summary>Consumer topology and failure routing (foundation 9.2): three retry tiers, then the dead-letter queue. Broker interaction is verified against a substitute channel.</summary>
public class RabbitMqTopologyTests
{
    [Fact]
    public void RetryTiers_AreThirtySeconds_TwoMinutes_TenMinutes()
    {
        RabbitMqTopology.RetryTiers.Select(t => (t.Name, t.Delay)).Should().Equal(
            ("30s", TimeSpan.FromSeconds(30)), ("2m", TimeSpan.FromMinutes(2)), ("10m", TimeSpan.FromMinutes(10)));
    }

    [Theory]
    [InlineData(1, "30s", false)]
    [InlineData(2, "2m", false)]
    [InlineData(3, "10m", false)]
    [InlineData(4, "10m", false)]
    [InlineData(5, null, true)]
    [InlineData(9, null, true)]
    public void Decide_PicksTheTierForTheAttempt_AndDeadLettersAtTheCap(int failedAttempts, string? tier, bool deadLetter)
    {
        var decision = RabbitMqTopology.Decide(failedAttempts);

        decision.DeadLetter.Should().Be(deadLetter);
        decision.Tier?.Name.Should().Be(tier);
        decision.NextAttempt.Should().Be(failedAttempts);
    }

    [Fact]
    public void QueueNames_FollowTheConvention()
    {
        var main = RabbitMqTopology.MainQueue("job-seeker-profile", "account-identity");

        main.Should().Be("q.job-seeker-profile.from.account-identity");
        RabbitMqTopology.RetryQueue(main, RabbitMqTopology.RetryTiers[1]).Should().Be(main + ".retry.2m");
        RabbitMqTopology.DeadLetterQueue(main).Should().Be(main + ".dlq");
    }

    [Fact]
    public async Task DeclareConsumer_DeclaresMainRetryTiersAndDeadLetterQueue_WithBindings()
    {
        var channel = Substitute.For<IChannel>();

        await RabbitMqTopology.DeclareConsumerAsync(channel, "job-seeker-profile", "account-identity", ExchangeNames.AccountIdentity,
            new[] { RoutingKeys.AccountApproved });

        var main = "q.job-seeker-profile.from.account-identity";
        foreach (var queue in new[] { main, main + ".dlq", main + ".retry.30s", main + ".retry.2m", main + ".retry.10m" })
        {
            await channel.Received().QueueDeclareAsync(queue, true, false, false, Arg.Any<IDictionary<string, object?>>(), false, false, Arg.Any<CancellationToken>());
        }

        await channel.Received().QueueBindAsync(main, ExchangeNames.AccountIdentity, RoutingKeys.AccountApproved, Arg.Any<IDictionary<string, object?>>(),
            false, Arg.Any<CancellationToken>());
        await channel.Received().QueueBindAsync(main + ".dlq", ExchangeNames.DeadLetter, main + ".dlq", Arg.Any<IDictionary<string, object?>>(),
            false, Arg.Any<CancellationToken>());
        await channel.Received().ExchangeDeclareAsync(ExchangeNames.AccountIdentity, ExchangeType.Topic, true, false, Arg.Any<IDictionary<string, object?>>(),
            false, false, Arg.Any<CancellationToken>());
        await channel.Received().ExchangeDeclareAsync(ExchangeNames.DeadLetter, ExchangeType.Direct, true, false, Arg.Any<IDictionary<string, object?>>(),
            false, false, Arg.Any<CancellationToken>());
    }

    private static BasicProperties Properties(object? attempt = null) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        Headers = attempt is null
            ? new Dictionary<string, object?> { ["type"] = "AccountApproved" }
            : new Dictionary<string, object?> { ["type"] = "AccountApproved", [RabbitMqTopology.AttemptHeader] = attempt }
    };

    [Theory]
    [InlineData(null, 0)]
    [InlineData(3, 3)]
    public void AttemptsSoFar_ReadsTheAttemptHeader(object? header, int expected) =>
        RabbitMqRetryRouter.AttemptsSoFar(Properties(header)).Should().Be(expected);

    [Fact]
    public void AttemptsSoFar_ToleratesStringHeadersAndGarbage()
    {
        RabbitMqRetryRouter.AttemptsSoFar(Properties("2"u8.ToArray())).Should().Be(2);
        RabbitMqRetryRouter.AttemptsSoFar(Properties("x"u8.ToArray())).Should().Be(0);
        RabbitMqRetryRouter.AttemptsSoFar(Properties(2L)).Should().Be(2);
    }

    [Theory]
    [InlineData(null, "q.c.from.s.retry.30s", 1)]
    [InlineData(1, "q.c.from.s.retry.2m", 2)]
    [InlineData(2, "q.c.from.s.retry.10m", 3)]
    [InlineData(4, "q.c.from.s.dlq", 5)]
    public async Task RouteFailure_RepublishesToTheRightQueueWithAnIncrementedAttempt_AndAcksTheOriginal(object? attempts, string target, int next)
    {
        var channel = Substitute.For<IChannel>();
        BasicProperties? published = null;
        await channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Do<BasicProperties>(p => published = p),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());

        await RabbitMqRetryRouter.RouteFailureAsync(channel, "q.c.from.s", 42, Properties(attempts), new byte[] { 1, 2 }, NullLogger.Instance, default);

        await channel.Received(1).BasicPublishAsync(ExchangeNames.DeadLetter, target, false, Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
        published!.Headers![RabbitMqTopology.AttemptHeader].Should().Be(next);
        published.Headers["type"].Should().Be("AccountApproved", "the original headers travel with the message");
        await channel.Received(1).BasicAckAsync(42, false, Arg.Any<CancellationToken>());
        await channel.DidNotReceiveWithAnyArgs().BasicNackAsync(default, default, default, default);
    }

    [Fact]
    public async Task RouteFailure_WhenTheRepublishFails_RequeuesInsteadOfLosingTheMessage()
    {
        var channel = Substitute.For<IChannel>();
        channel.When(c => c.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()))
            .Throw(new InvalidOperationException("channel closed"));

        await RabbitMqRetryRouter.RouteFailureAsync(channel, "q.c.from.s", 7, Properties(), new byte[] { 1 }, NullLogger.Instance, default);

        await channel.Received(1).BasicNackAsync(7, false, true, Arg.Any<CancellationToken>());
        await channel.DidNotReceiveWithAnyArgs().BasicAckAsync(default, default, default);
    }
}
