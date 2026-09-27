using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>One execution of a sync (handover 3.1 "Sync Run"); uses the mapping version in effect when it started.</summary>
public sealed class SyncRun : Entity<Guid>
{
    private SyncRun()
    {
    }

    internal SyncRun(Guid id, SyncTrigger trigger, int mappingVersion, DateTime startedAtUtc)
    {
        Id = id;
        Trigger = trigger;
        MappingVersion = mappingVersion;
        StartedAtUtc = startedAtUtc;
        Status = SyncRunStatus.Running;
    }

    public SyncTrigger Trigger { get; private set; }
    public SyncRunStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public int MappingVersion { get; private set; }
    public int Received { get; private set; }
    public int Accepted { get; private set; }
    public int Rejected { get; private set; }
    public string? ErrorCode { get; private set; }

    internal void Complete(int received, int accepted, int rejected, DateTime nowUtc)
    {
        Status = SyncRunStatus.Completed;
        Received = received;
        Accepted = accepted;
        Rejected = rejected;
        EndedAtUtc = nowUtc;
    }

    internal void Fail(string errorCode, DateTime nowUtc)
    {
        Status = SyncRunStatus.Failed;
        ErrorCode = errorCode;
        EndedAtUtc = nowUtc;
    }
}

/// <summary>
/// Proposed aggregate (handover 3.1): a partner's (external job site's) admission, sync configuration and sandbox. Realises US-2.5-02,
/// 3.1.3-05/13, 3.4.1-01/05.
/// </summary>
public sealed class ExternalJobSiteIntegration : AggregateRoot<Guid>
{
    private readonly List<SyncRun> _syncRuns = new();

    private ExternalJobSiteIntegration()
    {
    }

    public Guid PartnerAccountId { get; private set; }
    public SourcePlatform SourcePlatform { get; private set; } = null!;
    public AdmissionRecommendation Recommendation { get; private set; }
    public AdmissionStatus AdmissionStatus { get; private set; }
    public string? ApprovalBasis { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public SyncModels Models { get; private set; } = new(false, false);
    public SyncSchedule Schedule { get; private set; } = new(SyncMode.OnDemand, null);
    public AttributionVisibilityValue AttributionVisibility { get; private set; } = AttributionVisibilityValue.Public;
    public SandboxState Sandbox { get; private set; } = SandboxState.NotProvisioned;
    public DateTime? SandboxProvisionedAtUtc { get; private set; }
    public IntegrationStatus Status { get; private set; }
    public Guid? MappingId { get; private set; }

    public IReadOnlyCollection<SyncRun> SyncRuns => _syncRuns;

    public SyncRun? CurrentRun => _syncRuns.FirstOrDefault(r => r.Status == SyncRunStatus.Running);

    /// <summary>US-3.4.1-01 AC-01/02: a MoL/PEF-recommended site is auto-approved; others enter the review queue (handover Q-04).</summary>
    public static ExternalJobSiteIntegration Register(Guid id, Guid partnerAccountId, SourcePlatform platform, bool recommendedByMolPef, DateTime nowUtc)
    {
        var integration = new ExternalJobSiteIntegration
        {
            Id = id,
            PartnerAccountId = partnerAccountId,
            SourcePlatform = platform,
            Recommendation = recommendedByMolPef ? AdmissionRecommendation.Recommended : AdmissionRecommendation.NotRecommended,
            AdmissionStatus = recommendedByMolPef ? AdmissionStatus.Approved : AdmissionStatus.UnderReview,
            Status = IntegrationStatus.Registered
        };
        return integration;
    }

    /// <summary>INV-01 NOT_APPROVED path: an administrator (MoL/PEF authority) approves a non-recommended site.</summary>
    public void ApproveByMolPef(Actor actor, string approvalBasis, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.IntegrationAdminOnly, ErrorCodes.AdminOnly));
        Check(new BusinessRule(RuleCodes.IntegrationNotApproved, "Only a site under review can be approved.",
            AdmissionStatus != AdmissionStatus.UnderReview, ErrorCodes.NotApproved, BusinessRuleKind.Conflict));

        AdmissionStatus = AdmissionStatus.Approved;
        ApprovalBasis = approvalBasis;
        ApprovedBy = actor.Id;
    }

    /// <summary>INV-01: only an Approved (or auto-approved Recommended) admission can activate.</summary>
    public void Activate(Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.IntegrationAdminOnly, ErrorCodes.AdminOnly));
        Check(new BusinessRule(RuleCodes.IntegrationNotApproved, "The integration has not been approved yet.",
            AdmissionStatus != AdmissionStatus.Approved, ErrorCodes.NotApproved, BusinessRuleKind.Forbidden));

        Status = IntegrationStatus.Active;
    }

    public void Suspend(Actor actor, string reason)
    {
        _ = reason;
        Check(Rules.AdminOnly(actor, RuleCodes.IntegrationAdminOnly, ErrorCodes.AdminOnly));
        Check(new BusinessRule(RuleCodes.IntegrationNotActive, "Only an active integration can be suspended.",
            Status != IntegrationStatus.Active, ErrorCodes.IntegrationNotActive, BusinessRuleKind.Conflict));

        Status = IntegrationStatus.Suspended;
    }

    /// <summary>US-2.5-02 AC-04: enables pull and/or push. Requires Active and at least one model.</summary>
    public void Enable(bool pullEnabled, bool pushEnabled, Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, PartnerAccountId));
        Check(new BusinessRule(RuleCodes.IntegrationNotActive, "Only an active integration can be enabled.",
            Status != IntegrationStatus.Active, ErrorCodes.IntegrationNotActive, BusinessRuleKind.Conflict));
        Check(new BusinessRule(RuleCodes.IntegrationAtLeastOneModel, "At least one of pull or push must be enabled.",
            !pullEnabled && !pushEnabled, ErrorCodes.JobDataInvalidField, BusinessRuleKind.BusinessRule));

        Models = new SyncModels(pullEnabled, pushEnabled);
        var models = new List<string>();
        if (pullEnabled)
        {
            models.Add("pull");
        }

        if (pushEnabled)
        {
            models.Add("push");
        }

        Raise(new ExternalJobSiteIntegrationSupportedDomainEvent(Id, SourcePlatform.Id, models, nowUtc));
    }

    /// <summary>3.4.1-05 AC-03: a change while a sync is running still applies — only to the *next* run (the running one keeps its own
    /// snapshot in SyncRun.MappingVersion, which schedule changes never touch).</summary>
    public void ConfigureSyncSchedule(SyncMode mode, string? cron, Actor actor)
    {
        Check(Rules.OwnerOnly(actor, PartnerAccountId));
        Schedule = new SyncSchedule(mode, cron);
    }

    /// <summary>INV-03 VISIBILITY_OWNER_ONLY (3.1.3-13 AC-04).</summary>
    public void ConfigureAttributionVisibility(AttributionVisibilityValue value, Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, PartnerAccountId));

        AttributionVisibility = value;
        Raise(new AttributionVisibilityConfiguredDomainEvent(Id, actor.Id, value.ToString(), nowUtc));
    }

    /// <summary>INV-04 NO_ACTIVE_CREDENTIAL (3.1.3-05 AC-01/02/04): sandbox provisioning requires an active credential.</summary>
    public void ProvisionSandbox(bool hasActiveCredential, Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, PartnerAccountId));
        Check(new BusinessRule(RuleCodes.IntegrationNoActiveCredential, "An active API credential is required to provision a sandbox.",
            !hasActiveCredential, ErrorCodes.PartnerForbidden, BusinessRuleKind.Forbidden));

        Sandbox = SandboxState.Provisioned;
        SandboxProvisionedAtUtc = nowUtc;
    }

    public void AssignMapping(Guid mappingId) => MappingId = mappingId;

    /// <summary>INV-02 RUN_IN_PROGRESS: no concurrent run for the same integration. Snapshots the mapping version in effect now.</summary>
    public SyncRun StartSyncRun(SyncTrigger trigger, int mappingVersion, Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(actor, PartnerAccountId));
        Check(new BusinessRule(RuleCodes.IntegrationNotActive, "Only an active integration can run a sync.",
            Status != IntegrationStatus.Active, ErrorCodes.IntegrationNotActive, BusinessRuleKind.Conflict));
        Check(new BusinessRule(RuleCodes.IntegrationRunInProgress, "A sync run is already in progress for this integration.",
            CurrentRun is not null, ErrorCodes.RunInProgress, BusinessRuleKind.Conflict));

        var run = new SyncRun(Guid.NewGuid(), trigger, mappingVersion, nowUtc);
        _syncRuns.Add(run);
        return run;
    }

    public void CompleteSyncRun(Guid runId, int received, int accepted, int rejected, DateTime nowUtc)
    {
        var run = FindRun(runId);
        run.Complete(received, accepted, rejected, nowUtc);
    }

    public void FailSyncRun(Guid runId, string errorCode, DateTime nowUtc)
    {
        var run = FindRun(runId);
        run.Fail(errorCode, nowUtc);
    }

    private SyncRun FindRun(Guid runId) =>
        _syncRuns.FirstOrDefault(r => r.Id == runId)
        ?? throw new BusinessRuleViolationException(RuleCodes.IntegrationNoRunningSync, "No such sync run exists for this integration.",
            ErrorCodes.NotFound, BusinessRuleKind.BusinessRule);
}
