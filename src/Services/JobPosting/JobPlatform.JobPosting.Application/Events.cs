using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.JobPosting.Application.Events;

/// <summary>Maps BC-09 domain events to the seven published integration events (handover section 5.1).</summary>
public sealed class JobPostingEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        JobPostingCreatedDomainEvent e => new JobPostingCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobPostingId, e.ActorId, e.EmployerAccountId, e.Status.ToString(), e.TitleEn, e.CategoryCode, e.Skills, e.Visibility.ToString(),
            e.Source.ToString(), e.Location, e.SalaryMin, e.SalaryMax, c.AggregateVersion),

        JobPostingUpdatedDomainEvent e => new JobPostingUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobPostingId, e.EmployerAccountId, e.FromStatus.ToString(), e.ToStatus.ToString(), e.ActorId, e.ChangedFields, c.AggregateVersion),

        JobPostingRenewedDomainEvent e => new JobPostingRenewedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobPostingId, e.ActorId, e.NewDeadlineUtc, c.AggregateVersion),

        JobPostingStatusUpdatedDomainEvent e => new JobPostingStatusUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobPostingId, e.JobPostingId, e.EmployerAccountId, e.FromStatus.ToString(), e.ToStatus.ToString(), e.ActorId, e.Reason, c.AggregateVersion),

        FavoriteJobListCreatedDomainEvent e => new FavoriteJobListCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.FavoriteJobListId, e.ActorId, e.JobPostingId, c.AggregateVersion),

        InterestedListEntryCreatedDomainEvent e => new InterestedListEntryCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.InterestedListEntryId, e.ActorId, e.ReferenceType.ToString(), c.AggregateVersion),

        SavedSearchMatchedDomainEvent e => new SavedSearchMatchedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.SavedSearchId, e.JobSeekerId, e.JobPostingId, e.JobTitle, c.AggregateVersion),

        _ => null
    };
}

/// <summary>
/// US-3.2.2-04 AC-04: whenever a posting becomes (or stays) Active, checks every opted-in saved search against it. Must run inside the same
/// unit of work as the posting change (never after commit) so the SavedSearchMatched events it raises are captured by the outbox interceptor.
/// </summary>
public sealed class SavedSearchMatchEvaluator
{
    private readonly ISavedSearchRepository _searches;

    public SavedSearchMatchEvaluator(ISavedSearchRepository searches) => _searches = searches;

    public async Task EvaluateAsync(Domain.JobPosting posting, DateTime nowUtc, CancellationToken ct)
    {
        if (posting.Status != JobPostingStatus.Active)
        {
            return;
        }

        foreach (var search in await _searches.ListNotifyingAsync(ct))
        {
            search.EvaluateMatch(posting, nowUtc);
        }
    }
}

/// <summary>Inbox handler of <c>JobDataImported</c> (BC-02): upserts by <c>PlatformJobId</c>, publishing straight to Active (handover Q-04).</summary>
public sealed class ImportExternalJobHandler : IIntegrationEventHandler<JobDataImportedIntegrationEvent>
{
    private readonly IJobPostingRepository _postings;
    private readonly SavedSearchMatchEvaluator _matcher;
    private readonly TimeProvider _clock;

    public ImportExternalJobHandler(IJobPostingRepository postings, SavedSearchMatchEvaluator matcher, TimeProvider clock)
    {
        _postings = postings;
        _matcher = matcher;
        _clock = clock;
    }

    public async Task Handle(JobDataImportedIntegrationEvent e, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var fields = new JobPostingFields(
            new SharedKernel.Common.ValueObjects.LocalizedText(e.Title, e.Title), new SharedKernel.Common.ValueObjects.LocalizedText(e.Summary, e.Summary),
            e.Skills, "imported", ParseContractType(e.ContractType), null, Array.Empty<string>(), ParseWorkFormat(e.WorkFormat),
            Domain.JobLocation.Create(e.Location, null), null, null, null, Array.Empty<string>(),
            ApplicationDeadline.Create(e.DeadlineUtc ?? now.AddMonths(3), true), e.SourceUrl, null);

        var existing = await _postings.GetByPlatformJobIdAsync(e.PlatformJobId, ct);
        if (existing is not null)
        {
            existing.UpdateFromExternal(fields, existing.TaxonomyVersion, ContentHasher.Hash(fields), now);
            await _matcher.EvaluateAsync(existing, now, ct);
            return;
        }

        var source = JobSource.FromExternal(e.SourcePlatformId, e.PlatformJobId, e.SourceJobId, e.SourceUrl,
            string.Equals(e.AttributionVisibility, "public", StringComparison.OrdinalIgnoreCase));
        var posting = Domain.JobPosting.CreateFromExternal(source, fields, 0, ContentHasher.Hash(fields), now);
        _postings.Add(posting);
        await _matcher.EvaluateAsync(posting, now, ct);
    }

    private static ContractType ParseContractType(string value) => Enum.TryParse<ContractType>(value, true, out var v) ? v : ContractType.FullTime;

    private static WorkFormat ParseWorkFormat(string value) => Enum.TryParse<WorkFormat>(value, true, out var v) ? v : WorkFormat.Physical;
}

/// <summary>
/// Inbox handler of <c>JobPostAttributionUpdated</c> (BC-02, proposed - handover Q-06/gap G-06): applies partner-side edits/closures to the
/// imported posting they attribute to (deadline/description edits, or a status change for Closed/Deactivated/Deleted).
/// </summary>
public sealed class SyncExternalPostingHandler : IIntegrationEventHandler<JobPostAttributionUpdatedIntegrationEvent>
{
    private readonly IJobPostingRepository _postings;
    private readonly TimeProvider _clock;

    public SyncExternalPostingHandler(IJobPostingRepository postings, TimeProvider clock)
    {
        _postings = postings;
        _clock = clock;
    }

    public async Task Handle(JobPostAttributionUpdatedIntegrationEvent e, CancellationToken ct)
    {
        var posting = await _postings.GetByPlatformJobIdAsync(e.PlatformJobId, ct);
        if (posting is null)
        {
            return; // Out-of-order delivery (attribution update before the import): the inbox retries later.
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var actor = Actor.System(Guid.Empty);
        switch (e.ToStatus)
        {
            case "Closed" or "Deactivated" or "Deleted" when posting.Status is JobPostingStatus.Active or JobPostingStatus.Paused:
                posting.Expire(actor, now);
                break;
            case "Updated":
                if (e.DeadlineUtc is { } deadline && deadline > now && posting.Status != JobPostingStatus.Archived)
                {
                    posting.ExtendDeadline(ApplicationDeadline.Create(deadline, true), actor, now);
                }

                break;
        }
    }
}

/// <summary>Inbox handler of <c>JobOfferingSuspended</c> (BC-08): forces the posting into Paused (idempotent).</summary>
public sealed class ApplyAdminSuspensionHandler : IIntegrationEventHandler<JobOfferingSuspendedIntegrationEvent>
{
    private readonly IJobPostingRepository _postings;
    private readonly TimeProvider _clock;

    public ApplyAdminSuspensionHandler(IJobPostingRepository postings, TimeProvider clock)
    {
        _postings = postings;
        _clock = clock;
    }

    public async Task Handle(JobOfferingSuspendedIntegrationEvent e, CancellationToken ct)
    {
        var posting = await _postings.GetByIdAsync(e.JobPostingId, ct);
        posting?.ApplyAdminSuspension(e.Reason, _clock.GetUtcNow().UtcDateTime);
    }
}

/// <summary>Inbox handler of <c>PlatformTaxonomyUpdated</c> (BC-08): evicts the cached taxonomy so new submissions validate against the new version.
/// In-flight validations keep the version they already captured (handover section 5.2).</summary>
public sealed class RefreshTaxonomyCacheHandler : IIntegrationEventHandler<PlatformTaxonomyUpdatedIntegrationEvent>
{
    private readonly IJobPostingCache _cache;

    public RefreshTaxonomyCacheHandler(IJobPostingCache cache) => _cache = cache;

    public Task Handle(PlatformTaxonomyUpdatedIntegrationEvent e, CancellationToken ct) => _cache.RemoveAsync(CacheKeys.Taxonomy(e.TaxonomyType), ct);
}
