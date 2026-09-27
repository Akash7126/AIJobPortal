using JobPlatform.AuditLogging.Infrastructure;
using JobPlatform.AuditLogging.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

// Two in-process hosts started concurrently in one test process occasionally interfered (spurious 401s); run the API test classes one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace JobPlatform.AuditLogging.Api.IntegrationTests;

/// <summary>Hosts the real Audit Logging API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "Audit";

    // Background loops are driven explicitly by the tests (RunExportsAsync / RunRetentionAsync) for determinism.
    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["Retention:Enabled"] = "false";
        settings["Exports:Enabled"] = "false";
        return settings;
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

        await ProcessInboxAsync<AuditDbContext>();
    }

    public Task<int> RunExportsAsync() => Services.GetRequiredService<ExportGenerationService>().RunOnceAsync(CancellationToken.None);

    public Task<int> RunRetentionAsync() => Services.GetRequiredService<RetentionService>().RunOnceAsync(CancellationToken.None);
}
