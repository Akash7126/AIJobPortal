using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.CandidateSourcing.Application.Events;

/// <summary>Maps BC-11 domain events to the two published integration events (handover 5.1). Never carries candidate PII.</summary>
public sealed class CandidateSourcingEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        TalentPoolEntryCreatedDomainEvent e => new TalentPoolEntryCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.TalentPoolEntryId, e.JobPostingId, e.ActorId, e.EmployerAccountId, c.AggregateVersion),

        CandidateInsightComputedDomainEvent e => new CandidateInsightComputedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.CandidateInsightId, e.JobPostingId, e.ActorId, e.EmployerAccountId, e.CandidateProfileId, e.Availability, e.ExpectedSalary, e.FitScore,
            e.WithheldFields, c.AggregateVersion),

        _ => null
    };
}

/// <summary>Builds/refreshes the local candidate projection from BC-04's candidate-view API (handover gap Q-04: the catalogue event alone is too thin).</summary>
internal static class ProjectionUpdater
{
    public static async Task ApplyAsync(ICandidateProjectionRepository repository, IJobSeekerProfileApi profileApi, Guid profileId, Guid ownerAccountId,
        long aggregateVersion, TimeProvider clock, CancellationToken ct)
    {
        var view = await profileApi.GetCandidateViewAsync(profileId, ct);
        if (view is null)
        {
            return;
        }

        var projection = await repository.GetAsync(profileId, ct);
        if (projection is null)
        {
            projection = CandidateProjection.Create(profileId, ownerAccountId);
            repository.Add(projection);
        }

        projection.ApplyCandidateView(
            Enum.Parse<CandidateVisibility>(view.Visibility, ignoreCase: true), view.EmployerVisibilityOptIn, view.Deactivated, view.Skills,
            view.EducationLevel, view.YearsOfExperience, view.LocationCode, view.ExpectedSalaryMin, view.ExpectedSalaryMax, view.Availability,
            aggregateVersion, clock.GetUtcNow().UtcDateTime);
    }
}

public sealed class ProfileCreatedProjectionHandler : IIntegrationEventHandler<ProfileCreatedIntegrationEvent>
{
    private readonly ICandidateProjectionRepository _repository;
    private readonly IJobSeekerProfileApi _profileApi;
    private readonly TimeProvider _clock;

    public ProfileCreatedProjectionHandler(ICandidateProjectionRepository repository, IJobSeekerProfileApi profileApi, TimeProvider clock)
    {
        _repository = repository;
        _profileApi = profileApi;
        _clock = clock;
    }

    public Task Handle(ProfileCreatedIntegrationEvent integrationEvent, CancellationToken ct) =>
        ProjectionUpdater.ApplyAsync(_repository, _profileApi, integrationEvent.ProfileId, integrationEvent.OwnerAccountId, integrationEvent.AggregateVersion,
            _clock, ct);
}

public sealed class ProfileUpdatedProjectionHandler : IIntegrationEventHandler<ProfileUpdatedIntegrationEvent>
{
    private readonly ICandidateProjectionRepository _repository;
    private readonly IJobSeekerProfileApi _profileApi;
    private readonly TimeProvider _clock;

    public ProfileUpdatedProjectionHandler(ICandidateProjectionRepository repository, IJobSeekerProfileApi profileApi, TimeProvider clock)
    {
        _repository = repository;
        _profileApi = profileApi;
        _clock = clock;
    }

    /// <summary>ProfileUpdated has no owner account id, so the projection must already exist (created by ProfileCreated); an out-of-order delivery is
    /// tolerated by simply skipping - the row will be created once ProfileCreated eventually arrives.</summary>
    public async Task Handle(ProfileUpdatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var existing = await _repository.GetAsync(integrationEvent.ProfileId, ct);
        if (existing is null)
        {
            return;
        }

        await ProjectionUpdater.ApplyAsync(_repository, _profileApi, integrationEvent.ProfileId, existing.OwnerAccountId, integrationEvent.AggregateVersion,
            _clock, ct);
    }
}

/// <summary>CS.Privacy.DEACTIVATED_EXCLUDED (US-3.3.3-05 AC-02): a job seeker's account being suspended excludes them from every employer-facing path.</summary>
public sealed class ExcludeCandidateHandler : IIntegrationEventHandler<AccountSuspendedIntegrationEvent>
{
    private readonly ICandidateProjectionRepository _repository;
    private readonly TimeProvider _clock;

    public ExcludeCandidateHandler(ICandidateProjectionRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task Handle(AccountSuspendedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.ActorType != ActorType.JobSeeker)
        {
            return;
        }

        var projection = await _repository.GetByOwnerAccountIdAsync(integrationEvent.AccountId, ct);
        projection?.MarkDeactivated(_clock.GetUtcNow().UtcDateTime);
    }
}

/// <summary>Local verified-employer replica gating the candidate database search (US-3.3.3-04); idempotent, ignores a stale/duplicate delivery.</summary>
public sealed class MarkEmployerVerifiedHandler : IIntegrationEventHandler<EmployerVerificationApprovedIntegrationEvent>
{
    private readonly IVerifiedEmployerRepository _repository;
    private readonly TimeProvider _clock;

    public MarkEmployerVerifiedHandler(IVerifiedEmployerRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task Handle(EmployerVerificationApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (await _repository.GetAsync(integrationEvent.EmployerAccountId, ct) is not null)
        {
            return;
        }

        _repository.Add(VerifiedEmployer.Create(integrationEvent.EmployerAccountId, _clock.GetUtcNow().UtcDateTime));
    }
}

/// <summary>
/// A new match score means a previously cached candidate list may be stale: evict it so the next recommendation/ranking read re-fetches from BC-10
/// (handover section 5.2) instead of recomputing here.
/// </summary>
public sealed class MarkPostingMatchesStaleHandler : IIntegrationEventHandler<MatchScoreComputedIntegrationEvent>
{
    private readonly ICandidateSourcingCache _cache;

    public MarkPostingMatchesStaleHandler(ICandidateSourcingCache cache) => _cache = cache;

    public Task Handle(MatchScoreComputedIntegrationEvent integrationEvent, CancellationToken ct) =>
        _cache.RemoveAsync(CacheKeys.Recommendations(integrationEvent.JobPostingId), ct);
}
