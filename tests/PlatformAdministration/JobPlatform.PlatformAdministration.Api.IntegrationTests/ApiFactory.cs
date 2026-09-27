using JobPlatform.PlatformAdministration.Infrastructure;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.PlatformAdministration.Api.IntegrationTests;

/// <summary>Hosts the real Platform Administration API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS, sample taxonomy data).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "Admin";

    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["Seed:SampleData"] = "true";
        settings["UserDirectory:Provider"] = "Fake";
        settings["ReferenceUsage:Provider"] = "Fake";
        settings["ReferenceUsage:FakeInUse:0"] = "referenced-skill";
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

        await ProcessInboxAsync<AdminDbContext>();
    }

    public Task<int> PublishAsync() => PublishOutboxAsync<AdminDbContext>();

    public static JobPostingCreatedIntegrationEvent PostingCreated(Guid jobPostingId, string title = "Backend engineer", Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, jobPostingId, Guid.NewGuid(), Guid.NewGuid(), "draft", title, "software-development",
            new[] { "csharp" }, "public", "Employer", "ps-ramallah", null, null, 1);
}
