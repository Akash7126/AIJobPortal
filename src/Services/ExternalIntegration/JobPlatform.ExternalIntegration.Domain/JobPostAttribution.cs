using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>AGG-10: the source tag, backlink and sync state carried by an externally sourced post (US-3.1.3-09).</summary>
public sealed class JobPostAttribution : AggregateRoot<Guid>
{
    private JobPostAttribution()
    {
    }

    public Guid JobDataId { get; private set; }
    public string PlatformJobId { get; private set; } = string.Empty;
    public string SourcePlatformName { get; private set; } = string.Empty;
    public string? Backlink { get; private set; }
    public AttributionSyncState SyncState { get; private set; }
    public DateTime? DeadlineUtc { get; private set; }
    public string? Description { get; private set; }

    /// <summary>AC-01: tags a newly imported post with its source on first store.</summary>
    public static JobPostAttribution Tag(Guid id, Guid jobDataId, string platformJobId, string sourcePlatformName, string? backlink, Guid actorId,
        DateTime nowUtc)
    {
        var attribution = new JobPostAttribution
        {
            Id = id,
            JobDataId = jobDataId,
            PlatformJobId = platformJobId,
            SourcePlatformName = sourcePlatformName,
            Backlink = backlink,
            SyncState = AttributionSyncState.Active
        };
        attribution.Raise(new JobPostAttributionUpdatedDomainEvent(id, "New", "Active", actorId, platformJobId, null, null, nowUtc));
        return attribution;
    }

    public void ExtendDeadline(DateTime newDeadlineUtc, Actor actor, DateTime nowUtc)
    {
        CheckNotClosed();
        _ = actor;
        DeadlineUtc = newDeadlineUtc;
        Raise(new JobPostAttributionUpdatedDomainEvent(Id, SyncState.ToString(), "Updated", actor.Id, PlatformJobId, DeadlineUtc, Description, nowUtc));
    }

    public void EditDescription(string description, Actor actor, DateTime nowUtc)
    {
        CheckNotClosed();
        Description = description;
        Raise(new JobPostAttributionUpdatedDomainEvent(Id, SyncState.ToString(), "Updated", actor.Id, PlatformJobId, DeadlineUtc, Description, nowUtc));
    }

    public void Close(Actor actor, DateTime nowUtc) => Terminate(AttributionSyncState.Closed, actor, nowUtc);

    public void Deactivate(Actor actor, DateTime nowUtc) => Terminate(AttributionSyncState.Deactivated, actor, nowUtc);

    public void Delete(Actor actor, DateTime nowUtc) => Terminate(AttributionSyncState.Deleted, actor, nowUtc);

    private void Terminate(AttributionSyncState target, Actor actor, DateTime nowUtc)
    {
        Check(new BusinessRule(RuleCodes.AttributionNotActive, "Only an active attribution can change state.",
            SyncState != AttributionSyncState.Active, ErrorCodes.AttributionStateClosed, BusinessRuleKind.Conflict));

        var from = SyncState.ToString();
        SyncState = target;
        Raise(new JobPostAttributionUpdatedDomainEvent(Id, from, target.ToString(), actor.Id, PlatformJobId, DeadlineUtc, Description, nowUtc));
    }

    /// <summary>INV-08 STATE_CLOSED: an attributed post cannot be edited by its source once closed/deactivated/deleted.</summary>
    private void CheckNotClosed() =>
        Check(new BusinessRule(RuleCodes.AttributionStateClosed, "This post can no longer be edited by its source.",
            SyncState is AttributionSyncState.Closed or AttributionSyncState.Deactivated or AttributionSyncState.Deleted,
            ErrorCodes.AttributionStateClosed, BusinessRuleKind.Conflict));
}
