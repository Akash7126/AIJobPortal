using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Infrastructure;
using JobPlatform.GovernmentIntegration.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobPlatform.GovernmentIntegration.Api.IntegrationTests;

/// <summary>Test double for IMolRegistryClient: the real stub adapter always matches, but the Api tests need every outcome
/// (match / no-match / unavailable) to exercise the manual-review and escalation paths over HTTP.</summary>
public sealed class TestMolRegistryClient : IMolRegistryClient
{
    public SourceCallOutcome NextEmployerOutcome { get; set; } = SourceCallOutcome.Match;
    public string? NextErrorCode { get; set; }

    public Task<EmployerVerificationCheckResult> VerifyEmployerAsync(Submission submission, CancellationToken ct) =>
        Task.FromResult(new EmployerVerificationCheckResult(NextEmployerOutcome, NextErrorCode));

    public Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct) =>
        Task.FromResult(new GovernmentDataCheckResult(SourceCallOutcome.Match, new Dictionary<string, string> { ["k"] = "v" }, null));

    public Task<SourceSyncResult> SyncAsync(CancellationToken ct) => Task.FromResult(new SourceSyncResult(true, "snapshot-1", null));
}

/// <summary>Hosts the real Government Integration API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "Government";

    public TestMolRegistryClient Mol => (TestMolRegistryClient)Services.GetRequiredService<IMolRegistryClient>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IMolRegistryClient>();
        services.AddSingleton<IMolRegistryClient, TestMolRegistryClient>();
    }

    /// <summary>Delivers an integration event exactly like the broker consumer would: persist to the inbox, then process it.</summary>
    public async Task IngestAsync(params IIntegrationEvent[] events)
    {
        using var scope = Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IInboxWriter>();
        foreach (var e in events)
        {
            await writer.TryAddAsync(new InboxMessage
            {
                MessageId = e.MessageId,
                ConsumerName = DependencyInjection.ConsumerName,
                Type = e.EventType,
                Version = e.Version,
                Payload = IntegrationJson.Serialize(e),
                ReceivedOnUtc = Clock.GetUtcNow().UtcDateTime,
                NextAttemptUtc = Clock.GetUtcNow().UtcDateTime,
                Status = InboxStatus.Pending
            });
        }

        await ProcessInboxAsync<GovernmentIntegrationDbContext>();
    }

    public Task<int> PublishAsync() => PublishOutboxAsync<GovernmentIntegrationDbContext>();

    public static AccountCreatedIntegrationEvent EmployerAccountCreated(Guid employerAccountId, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, employerAccountId, Guid.NewGuid(), ActorType.Employer, 1);
}
