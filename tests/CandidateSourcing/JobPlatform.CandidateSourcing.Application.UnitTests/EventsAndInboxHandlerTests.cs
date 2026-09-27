using JobPlatform.CandidateSourcing.Application.Events;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.CandidateSourcing.Application.UnitTests;

public class EventsAndInboxHandlerTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DomainEventContext Context = new("agg", 3, Guid.NewGuid(), null);

    [Fact]
    public void EventMapper_MapsTalentPoolEntryCreated()
    {
        var e = new TalentPoolEntryCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), At);

        var mapped = new CandidateSourcingEventMapper().Map(e, Context).Should().BeOfType<TalentPoolEntryCreatedIntegrationEvent>().Which;

        mapped.TalentPoolEntryId.Should().Be(e.TalentPoolEntryId);
        mapped.AggregateVersion.Should().Be(3);
    }

    [Fact]
    public void EventMapper_MapsCandidateInsightComputed()
    {
        var e = new CandidateInsightComputedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Immediate", 3000m,
            80m, new[] { "salary" }, At);

        var mapped = new CandidateSourcingEventMapper().Map(e, Context).Should().BeOfType<CandidateInsightComputedIntegrationEvent>().Which;

        mapped.Availability.Should().Be("Immediate");
        mapped.WithheldFields.Should().Equal("salary");
    }

    [Fact]
    public async Task ProfileCreatedProjectionHandler_HydratesFromCandidateView()
    {
        var store = new FakeStore();
        var profiles = Substitute.For<IJobSeekerProfileApi>();
        var profileId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        profiles.GetCandidateViewAsync(profileId, Arg.Any<CancellationToken>())
            .Returns(new CandidateViewDto(profileId, "Public", false, false, new[] { "skills" }, new[] { "sql" }, "Bachelor", 3m, "PS-RAM",
                Array.Empty<string>(), 2000m, 3000m, "Immediate", At));
        var handler = new ProfileCreatedProjectionHandler(store, profiles, Kit.Clock());

        await handler.Handle(new ProfileCreatedIntegrationEvent(Guid.NewGuid(), At, Guid.NewGuid(), null, profileId, Guid.NewGuid(), owner, "PS-RAM", 1), default);

        var projection = store.Projections.Should().ContainSingle().Which;
        projection.OwnerAccountId.Should().Be(owner);
        projection.Skills.Should().Equal("sql");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-05")]
    [Trait("AC", "AC-02")]
    public async Task ExcludeCandidateHandler_OnAccountSuspended_MarksProjectionDeactivated()
    {
        var store = new FakeStore();
        var owner = Guid.NewGuid();
        var projection = CandidateProjection.Create(Guid.NewGuid(), owner);
        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, Array.Empty<string>(), null, null, null, null, null, null, 1, At);
        store.Projections.Add(projection);
        var handler = new ExcludeCandidateHandler(store, Kit.Clock());

        await handler.Handle(new AccountSuspendedIntegrationEvent(Guid.NewGuid(), At, Guid.NewGuid(), null, owner, Guid.NewGuid(), ActorType.JobSeeker,
            "policy", "Deactivated", 2), default);

        projection.Deactivated.Should().BeTrue();
    }

    [Fact]
    public async Task ExcludeCandidateHandler_IgnoresNonJobSeekerSuspensions()
    {
        var store = new FakeStore();
        var owner = Guid.NewGuid();
        var projection = CandidateProjection.Create(Guid.NewGuid(), owner);
        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, Array.Empty<string>(), null, null, null, null, null, null, 1, At);
        store.Projections.Add(projection);
        var handler = new ExcludeCandidateHandler(store, Kit.Clock());

        await handler.Handle(new AccountSuspendedIntegrationEvent(Guid.NewGuid(), At, Guid.NewGuid(), null, owner, Guid.NewGuid(), ActorType.Employer,
            "policy", "Deactivated", 2), default);

        projection.Deactivated.Should().BeFalse();
    }

    [Fact]
    public async Task MarkEmployerVerifiedHandler_IsIdempotentOnRedelivery()
    {
        var store = new FakeStore();
        var handler = new MarkEmployerVerifiedHandler(store, Kit.Clock());
        var employer = Guid.NewGuid();
        var e = new EmployerVerificationApprovedIntegrationEvent(Guid.NewGuid(), At, Guid.NewGuid(), null, Guid.NewGuid(), employer, Guid.NewGuid(), "Automatic", 1);

        await handler.Handle(e, default);
        await handler.Handle(e, default);

        store.VerifiedEmployers.Should().ContainSingle();
    }

    [Fact]
    public async Task MarkPostingMatchesStaleHandler_EvictsRecommendationsCache()
    {
        var cache = new FakeCache();
        var jobPostingId = Guid.NewGuid();
        cache.Items[CacheKeys.Recommendations(jobPostingId)] = new object();
        var handler = new MarkPostingMatchesStaleHandler(cache);

        await handler.Handle(new JobPlatform.SharedKernel.IntegrationEvents.AiMatching.MatchScoreComputedIntegrationEvent(Guid.NewGuid(), At, Guid.NewGuid(),
            null, Guid.NewGuid(), jobPostingId, Guid.NewGuid(), 80m, "v1", 1), default);

        cache.Items.Should().NotContainKey(CacheKeys.Recommendations(jobPostingId));
    }
}
