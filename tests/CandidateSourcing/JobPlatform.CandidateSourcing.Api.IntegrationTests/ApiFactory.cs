using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.CandidateSourcing.Infrastructure.Adapters;
using JobPlatform.CandidateSourcing.Infrastructure.Persistence;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.CandidateSourcing.Api.IntegrationTests;

/// <summary>Hosts the real Candidate Sourcing API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS, fake downstream
/// clients to BC-09/BC-04/BC-10).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "CandidateSourcing";

    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["JobPostingClient:Provider"] = "Fake";
        settings["JobSeekerProfileClient:Provider"] = "Fake";
        settings["AiMatchingClient:Provider"] = "Fake";
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
                ConsumerName = JobPlatform.CandidateSourcing.Infrastructure.DependencyInjection.ConsumerName,
                Type = e.EventType,
                Version = e.Version,
                Payload = IntegrationJson.Serialize(e),
                ReceivedOnUtc = Clock.GetUtcNow().UtcDateTime,
                NextAttemptUtc = Clock.GetUtcNow().UtcDateTime,
                Status = InboxStatus.Pending
            });
        }

        await ProcessInboxAsync<CandidateSourcingDbContext>();
    }

    public static EmployerVerificationApprovedIntegrationEvent EmployerVerified(Guid employerAccountId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), employerAccountId, Guid.NewGuid(), "ManualMoL", 1);

    public static ProfileCreatedIntegrationEvent ProfileCreated(Guid profileId, Guid ownerAccountId) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, profileId, Guid.NewGuid(), ownerAccountId, "Ramallah", 1);

    /// <summary>Registers the employer as verified (via inbox ingestion, like the real consumer) so candidate-database search is allowed.</summary>
    public async Task<Guid> VerifiedEmployerAsync()
    {
        var employerId = Guid.NewGuid();
        await IngestAsync(EmployerVerified(employerId));
        return employerId;
    }

    /// <summary>Seeds BC-04's fake candidate view and delivers ProfileCreated so this BC's own projection is populated (US-3.3.3-04/05).</summary>
    public async Task<Guid> PublicCandidateAsync(IReadOnlyList<string>? skills = null, string? educationLevel = "Bachelor",
        decimal? yearsOfExperience = 3, string? locationCode = "Ramallah", decimal? salaryMin = 1000, decimal? salaryMax = 2000,
        string? availability = "Immediate")
    {
        var profileId = Guid.NewGuid();
        var ownerAccountId = Guid.NewGuid();
        FakeJobSeekerProfileApiClient.Seed(new CandidateViewDto(profileId, "Public", false, false,
            new[] { "skills", "education", "experience", "location", "salary", "availability" }, skills ?? new[] { "C#", "SQL" }, educationLevel,
            yearsOfExperience, locationCode, Array.Empty<string>(), salaryMin, salaryMax, availability, DateTime.UtcNow));
        await IngestAsync(ProfileCreated(profileId, ownerAccountId));
        return profileId;
    }

    public static PostingForMatchingDto Posting(Guid jobPostingId, Guid employerAccountId, string title = "Backend Engineer") => new(jobPostingId,
        employerAccountId, "Active", false, 1, title, "software-development", null, new[] { "C#" }, "Bachelor", Array.Empty<string>(), "Ramallah",
        "Ramallah", "Hybrid", 1, 5, 1000, 2000);
}
