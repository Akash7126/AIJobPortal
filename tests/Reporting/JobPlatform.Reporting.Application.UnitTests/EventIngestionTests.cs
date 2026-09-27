using JobPlatform.Reporting.Application.Ingestion;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobPlatform.Reporting.Application.UnitTests;

public class EventCatalogTests
{
    [Fact]
    public void ConsumedEvents_ListsExactlyFortyFive() =>
        EventCatalog.ConsumedEvents.Should().HaveCount(45);

    [Fact]
    public void ConsumedEvents_ListsDistinctTypes() =>
        EventCatalog.ConsumedEvents.Should().OnlyHaveUniqueItems();

    [Fact]
    public void Describe_AccountCreated_MapsToRegistrationActivity()
    {
        var e = new AccountCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), ActorType.JobSeeker, 1);

        var description = EventCatalog.Describe(e);

        description.Should().NotBeNull();
        description!.ActivityType.Should().Be(ActivityTypes.Registration);
        description.ActorType.Should().Be("JobSeeker");
    }

    [Fact]
    public void Describe_JobPostingCreated_MapsToJobPostingActivity()
    {
        var e = new JobPostingCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "active", "Developer", "it", new[] { "c#" }, "public", "Employer", "Ramallah", 1000, 2000, 1);

        var description = EventCatalog.Describe(e);

        description!.ActivityType.Should().Be(ActivityTypes.JobPosting);
        description.ActorType.Should().Be("Employer");
    }

    [Fact]
    public void Describe_MatchScoreComputed_HasNoPersonalActor()
    {
        var e = new MatchScoreComputedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 80, "1", 1);

        var description = EventCatalog.Describe(e);

        description!.ActorId.Should().BeNull();
        description.ActivityType.Should().Be(ActivityTypes.Matching);
    }

    [Fact]
    public void Describe_TalentPoolEntryCreated_MapsToCandidateSearchActivity()
    {
        var e = new TalentPoolEntryCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1);

        EventCatalog.Describe(e)!.ActivityType.Should().Be(ActivityTypes.CandidateSearch);
    }

    [Fact]
    public void Describe_NotificationSent_HasNoPersonalActor()
    {
        var e = new NotificationSentIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Email", "OtpRequested", Guid.NewGuid(),
            "***@example.com", null, "Sent", 1);

        EventCatalog.Describe(e)!.ActorId.Should().BeNull();
    }
}

public class EventIngestionServiceTests
{
    private static EventIngestionService Service(FakeFactStore store) =>
        new(store, Options.Create(new ReportingOptions { ActorKeySalt = "test-salt" }), NullLogger<EventIngestionService>.Instance);

    private static AccountCreatedIntegrationEvent SampleEvent(Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), ActorType.JobSeeker, 1);

    [Fact]
    public async Task RecordAsync_NewEvent_AddsFactEventAndDailyRollups()
    {
        var store = new FakeFactStore();
        var e = SampleEvent();

        var recorded = await Service(store).RecordAsync(e, CancellationToken.None);

        recorded.Should().BeTrue();
        store.Events.Should().ContainSingle().Which.MessageId.Should().Be(e.MessageId);
        store.Daily.Keys.Should().Contain(k => k.Item2 == "event.AccountCreated");
        store.Daily.Keys.Should().Contain(k => k.Item2 == "activity." + ActivityTypes.Registration);
    }

    [Fact]
    public async Task RecordAsync_DuplicateMessageId_IsIgnored()
    {
        var store = new FakeFactStore();
        var e = SampleEvent();
        await Service(store).RecordAsync(e, CancellationToken.None);

        var recordedAgain = await Service(store).RecordAsync(e, CancellationToken.None);

        recordedAgain.Should().BeFalse();
        store.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task RecordAsync_PseudonymisesTheActor_NeverStoresTheRawId()
    {
        var store = new FakeFactStore();
        var e = SampleEvent();

        await Service(store).RecordAsync(e, CancellationToken.None);

        store.Events.Single().ActorKey.Should().NotBe(e.ActorId.ToString());
    }
}

public class JobPostingProjectorTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task JobPostingCreatedProjector_OpensPostingAndRecordsDemandSkills()
    {
        var store = new FakeFactStore();
        var jobId = Guid.NewGuid();
        var e = new JobPostingCreatedIntegrationEvent(Guid.NewGuid(), At, Guid.NewGuid(), null, jobId, Guid.NewGuid(), Guid.NewGuid(), "active", "Developer",
            "it", new[] { "C#", "SQL" }, "public", "Employer", "Ramallah", 1000, 2000, 1);

        await new JobPostingCreatedProjector(store).ProjectAsync(e, CancellationToken.None);

        store.Postings[jobId].Status.Should().Be("active");
        store.Postings[jobId].Title.Should().Be("Developer");
        store.Skills.Should().HaveCount(2).And.Contain(s => s.Skill == "c#" && s.Side == FactSkillDemand.Demand);
    }

    [Fact]
    public async Task JobPostingUpdatedProjector_OutOfOrderVersion_IsIgnored()
    {
        var store = new FakeFactStore();
        var jobId = Guid.NewGuid();
        await store.GetOrOpenPostingAsync(jobId, At, CancellationToken.None);
        store.Postings[jobId].ApplyStatus("active", At, version: 5);

        var stale = new JobPostingUpdatedIntegrationEvent(Guid.NewGuid(), At.AddMinutes(1), Guid.NewGuid(), null, jobId, Guid.NewGuid(), "active", "paused",
            Guid.NewGuid(), Array.Empty<string>(), 3);
        await new JobPostingUpdatedProjector(store).ProjectAsync(stale, CancellationToken.None);

        store.Postings[jobId].Status.Should().Be("active");
    }

    [Fact]
    public async Task JobOfferingSuspendedProjector_MarksThePostingPaused()
    {
        var store = new FakeFactStore();
        var jobId = Guid.NewGuid();
        await store.GetOrOpenPostingAsync(jobId, At, CancellationToken.None);
        store.Postings[jobId].ApplyStatus("active", At, version: 1);

        var e = new JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration.JobOfferingSuspendedIntegrationEvent(Guid.NewGuid(), At.AddMinutes(1),
            Guid.NewGuid(), null, JobOfferingId: Guid.NewGuid(), JobPostingId: jobId, ActorId: Guid.NewGuid(), Reason: "Misleading content", AggregateVersion: 2);
        await new JobOfferingSuspendedProjector(store).ProjectAsync(e, CancellationToken.None);

        store.Postings[jobId].Status.Should().Be("paused");
    }
}
