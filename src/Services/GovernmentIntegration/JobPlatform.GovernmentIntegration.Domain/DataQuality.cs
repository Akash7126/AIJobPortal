using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum DataQualityStatus
{
    Pending,
    Running,
    Completed
}

/// <summary>AGG-36: the cleansed/deduplicated/standardised state of a migrated batch (handover section 3.6, story US-6.1-04).</summary>
public sealed class DataQuality : AggregateRoot<Guid>
{
    private bool _recorded;

    private DataQuality()
    {
    }

    public Guid MigrationRunId { get; private set; }
    public Guid BatchId { get; private set; }
    public string SnapshotVersion { get; private set; } = string.Empty;
    public DataQualityStatus Status { get; private set; }
    public int IssuesResolved { get; private set; }
    public int DuplicatesRemoved { get; private set; }
    public int FormatsStandardized { get; private set; }
    public int RecordsChecked { get; private set; }
    public int RecordsRejected { get; private set; }

    /// <summary>INV-13: a cleansing run works on the snapshot version it started with (snapshot isolation).</summary>
    public static DataQuality Start(Guid id, Guid migrationRunId, Guid batchId, string snapshotVersion) =>
        new()
        {
            Id = id,
            MigrationRunId = migrationRunId,
            BatchId = batchId,
            SnapshotVersion = snapshotVersion,
            Status = DataQualityStatus.Running
        };

    /// <summary>AC-02: quality-issue resolution, deduplication and format standardisation are recorded as the pipeline runs; all three must
    /// be recorded (even as zero) before <see cref="Complete"/>.</summary>
    public void Record(int issuesResolved, int duplicatesRemoved, int formatsStandardized, int recordsChecked, int recordsRejected)
    {
        Check(new BusinessRule(RuleCodes.DataQualitySnapshotIsolation, "Progress can only be recorded while the run is active.",
            Status != DataQualityStatus.Running, ErrorCodes.DataQualitySnapshotIsolation, BusinessRuleKind.Conflict));
        IssuesResolved += issuesResolved;
        DuplicatesRemoved += duplicatesRemoved;
        FormatsStandardized += formatsStandardized;
        RecordsChecked += recordsChecked;
        RecordsRejected += recordsRejected;
        _recorded = true;
    }

    public void Complete(DateTime nowUtc)
    {
        Check(new BusinessRule(RuleCodes.DataQualitySnapshotIsolation, "The cleansing stages must be recorded before the run can complete.",
            Status != DataQualityStatus.Running || !_recorded, ErrorCodes.DataQualitySnapshotIsolation, BusinessRuleKind.Conflict));
        var fromStatus = Status.ToString();
        Status = DataQualityStatus.Completed;
        Raise(new DataQualityUpdatedDomainEvent(Id, fromStatus, Status.ToString(), MigrationRunId, RecordsChecked, RecordsRejected, nowUtc));
    }
}
