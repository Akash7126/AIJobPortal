using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AiMatching.Application.UnitTests;

public class MatchingConfigurationHandlerTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static ICurrentUser AdminUser()
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(Guid.NewGuid());
        user.ActorType.Returns(ActorType.Administrator);
        return user;
    }

    private static FakeTimeProvider Clock() => new(new DateTimeOffset(At));

    [Fact]
    public async Task ConfigureMatchThreshold_Valid_UpdatesConfigurationAndSucceeds()
    {
        var repository = Substitute.For<IMatchingConfigurationRepository>();
        var configuration = MatchingConfiguration.CreateDefault(At);
        repository.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(configuration);
        var handler = new ConfigureMatchThresholdHandler(repository, AdminUser(), Clock());

        var result = await handler.Handle(new ConfigureMatchThresholdCommand(80), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        configuration.MatchThresholdPercent.Should().Be(80);
    }

    [Fact]
    public async Task ConfigureMatchThreshold_ConfigurationNotInitialised_ReturnsNotFound()
    {
        var repository = Substitute.For<IMatchingConfigurationRepository>();
        repository.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns((MatchingConfiguration?)null);
        var handler = new ConfigureMatchThresholdHandler(repository, AdminUser(), Clock());

        var result = await handler.Handle(new ConfigureMatchThresholdCommand(80), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ConfigureMatchThreshold_StaleIfMatch_ReturnsPreconditionFailed()
    {
        var repository = Substitute.For<IMatchingConfigurationRepository>();
        var configuration = MatchingConfiguration.CreateDefault(At);
        repository.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(configuration);
        var handler = new ConfigureMatchThresholdHandler(repository, AdminUser(), Clock());

        var result = await handler.Handle(new ConfigureMatchThresholdCommand(80, ETag.From(new byte[] { 9, 9, 9 })), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.PreconditionFailed);
    }

    [Fact]
    public async Task ConfigureMatchThreshold_MatchingIfMatch_Succeeds()
    {
        var repository = Substitute.For<IMatchingConfigurationRepository>();
        var configuration = MatchingConfiguration.CreateDefault(At);
        repository.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(configuration);
        var handler = new ConfigureMatchThresholdHandler(repository, AdminUser(), Clock());

        var result = await handler.Handle(new ConfigureMatchThresholdCommand(80, ETag.From(configuration.RowVersion)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConfigureMatchingParameter_Valid_UpdatesWeights()
    {
        var repository = Substitute.For<IMatchingConfigurationRepository>();
        var configuration = MatchingConfiguration.CreateDefault(At);
        repository.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(configuration);
        var handler = new ConfigureMatchingParameterHandler(repository, AdminUser(), Clock());

        var result = await handler.Handle(new ConfigureMatchingParameterCommand(50, 10, 10, 10, 10, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        configuration.Weights.SkillOverlap.Should().Be(50);
    }

    [Fact]
    public async Task ConfigureMatchingParameter_ConfigurationNotInitialised_ReturnsNotFound()
    {
        var repository = Substitute.For<IMatchingConfigurationRepository>();
        repository.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns((MatchingConfiguration?)null);
        var handler = new ConfigureMatchingParameterHandler(repository, AdminUser(), Clock());

        var result = await handler.Handle(new ConfigureMatchingParameterCommand(50, 10, 10, 10, 10, 10), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}

public class ConfigurationChangedHandlerTests
{
    [Fact]
    public async Task Handle_InvalidatesTheCachedConfiguration()
    {
        var provider = Substitute.For<IMatchingConfigurationProvider>();
        var handler = new ConfigurationChangedHandler(provider);

        await handler.Handle(new MatchingConfigurationChangedDomainEvent(DateTime.UtcNow, 2, "Threshold"), CancellationToken.None);

        await provider.Received(1).InvalidateAsync(Arg.Any<CancellationToken>());
    }
}
