using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>AGG-09: a job record pulled from / pushed by a partner, before it is imported as a platform job (US-3.1.3-03, US-3.4.1-02).</summary>
public sealed class JobData : AggregateRoot<Guid>
{
    private JobData()
    {
    }

    public Guid IntegrationId { get; private set; }
    public Guid SourcePlatformId { get; private set; }
    public string SourceJobId { get; private set; } = string.Empty;
    public string? PlatformJobId { get; private set; }
    public StandardJob? Standardized { get; private set; }
    public string RawPayload { get; private set; } = "{}";
    public JobDataStatus Status { get; private set; }
    public JobDataModel Model { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public Guid? SyncRunId { get; private set; }

    /// <summary>INV-05 SOURCE_IDENTITY_REQUIRED: a pushed/pulled job must carry a valid source platform identity.</summary>
    public static JobData Receive(Guid id, Guid integrationId, Guid sourcePlatformId, string sourceJobId, string rawPayload, JobDataModel model,
        DateTime nowUtc, Guid? syncRunId = null)
    {
        if (sourcePlatformId == Guid.Empty || string.IsNullOrWhiteSpace(sourceJobId))
        {
            throw new BusinessRuleViolationException(RuleCodes.JobDataSourceIdentityRequired, "A valid source platform identity is required.",
                ErrorCodes.JobDataInvalidField, BusinessRuleKind.InvalidInput);
        }

        return new JobData
        {
            Id = id,
            IntegrationId = integrationId,
            SourcePlatformId = sourcePlatformId,
            SourceJobId = sourceJobId,
            RawPayload = rawPayload,
            Model = model,
            Status = JobDataStatus.Received,
            ReceivedAtUtc = nowUtc,
            SyncRunId = syncRunId
        };
    }

    /// <summary>INV-06 DUPLICATE_SOURCE_JOB: a re-push updates the existing record instead of duplicating it (3.1.3-03 AC-04).</summary>
    public void UpdateRaw(string rawPayload, Guid? syncRunId, DateTime nowUtc)
    {
        RawPayload = rawPayload;
        SyncRunId = syncRunId;
        ReceivedAtUtc = nowUtc;
        Status = JobDataStatus.Received;
    }

    public void Standardize(StandardJob standardJob)
    {
        Standardized = standardJob;
        Status = JobDataStatus.Standardized;
    }

    /// <summary>INV-07 REQUIRED_FIELD_MISSING: title, summary and at least one skill are required. Assigns PlatformJobId on first accept
    /// (handover Q-06: BC-02 issues it and BC-09 reuses it); raises JobDataImported (3.1.3-03 AC-05; 3.4.1-02 AC-04).</summary>
    public void Accept(Guid actorId, string attributionVisibility, DateTime nowUtc)
    {
        var missingRequired = Standardized is null || string.IsNullOrWhiteSpace(Standardized.Title) || string.IsNullOrWhiteSpace(Standardized.Summary)
                               || Standardized.Skills.Count == 0;
        Check(new BusinessRule(RuleCodes.JobDataRequiredFieldMissing, "Title, summary and at least one skill are required to accept a job.",
            missingRequired, ErrorCodes.JobDataInvalidField, BusinessRuleKind.InvalidInput));

        var isUpdate = PlatformJobId is not null;
        PlatformJobId ??= Guid.NewGuid().ToString("N");
        Status = JobDataStatus.Accepted;

        var standardized = Standardized!;
        Raise(new JobDataImportedDomainEvent(Id, SourcePlatformId, actorId, PlatformJobId, SourceJobId, standardized.Title, standardized.Summary,
            standardized.Skills, standardized.ContractType, standardized.WorkFormat, standardized.DeadlineUtc, standardized.Location,
            standardized.SourceUrl, attributionVisibility, isUpdate, nowUtc));
    }

    public void Reject() => Status = JobDataStatus.Rejected;
}
