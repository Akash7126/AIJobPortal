using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.JobSeekerProfile.Api.IntegrationTests;

/// <summary>Hosts the real Job Seeker Profile API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS, local file storage
/// under a per-run temp folder, no-op malware scanner, fake account-identity client).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    private readonly string _fileStorageRoot = Path.Combine(Path.GetTempPath(), "jsrpm-tests-" + Guid.NewGuid().ToString("N"));

    protected override string ConnectionStringName => "JobSeekerProfile";

    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["FileStorage:Provider"] = "Local";
        settings["FileStorage:LocalPath"] = _fileStorageRoot;
        settings["AccountIdentity:Provider"] = "Fake";
        settings["Api:PublicBaseUrl"] = "https://jobs.jobplatform.local";
        return settings;
    }

    public static object CreateProfileBody(string fullName = "Layla Haddad", string email = "layla@example.org", string mobile = "+970590000001",
        string gender = "Female") => new { fullName, email, mobileNumber = mobile, gender };

    public static AccountApprovedIntegrationEvent JobSeekerAccountApproved(Guid accountId, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(), ActorType.JobSeeker, 1);

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
                ConsumerName = JobPlatform.JobSeekerProfile.Infrastructure.DependencyInjection.ConsumerName,
                Type = e.EventType,
                Version = e.Version,
                Payload = IntegrationJson.Serialize(e),
                ReceivedOnUtc = Clock.GetUtcNow().UtcDateTime,
                NextAttemptUtc = Clock.GetUtcNow().UtcDateTime,
                Status = InboxStatus.Pending
            });
        }

        await ProcessInboxAsync<JobSeekerProfileDbContext>();
    }

    /// <summary>Records the account as an active known job seeker (INV-01) and returns a JobSeeker token for it.</summary>
    public async Task<string> ActiveJobSeekerAsync(Guid? id = null)
    {
        var accountId = id ?? Guid.NewGuid();
        await IngestAsync(JobSeekerAccountApproved(accountId));
        return TestTokens.JobSeeker(accountId);
    }
}
