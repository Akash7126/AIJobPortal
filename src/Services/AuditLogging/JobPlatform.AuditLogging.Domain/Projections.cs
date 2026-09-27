using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain;

/// <summary>
/// Projection state machine per (partner, platform job): Pending, Synced, Failed, Archived (US-3.1.3-10, US-3.4.1-04). Failed goes back through Pending on retry
/// (AL.Sync.RETRY_VIA_PENDING). Illegal or stale transitions are ignored (out-of-order delivery) and reported to the caller as false.
/// </summary>
public sealed class SyncJobStatus : Entity<string>
{
    private SyncJobStatus()
    {
    }

    public Guid SourcePlatformId { get; private set; }
    public Guid OwnerId { get; private set; }
    public SyncStatus Status { get; private set; }
    public string? ReasonCode { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public long LastEventVersion { get; private set; }

    /// <summary>A job the platform has received but not yet confirmed.</summary>
    public static SyncJobStatus Received(string platformJobId, Guid sourcePlatformId, Guid ownerId, DateTime nowUtc)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(platformJobId), AuditRuleCodes.InvalidEntry, "A platform job id is required.");
        return new SyncJobStatus { Id = platformJobId, SourcePlatformId = sourcePlatformId, OwnerId = ownerId, Status = SyncStatus.Pending, UpdatedAtUtc = nowUtc };
    }

    private bool Stale(long version) => version != 0 && version < LastEventVersion;

    private bool Move(SyncStatus to, string? reason, DateTime nowUtc, long version)
    {
        Status = to;
        ReasonCode = reason;
        UpdatedAtUtc = nowUtc;
        LastEventVersion = Math.Max(LastEventVersion, version);
        return true;
    }

    /// <summary>Pending to Synced. Synced to Synced is an idempotent no-op that still counts as applied.</summary>
    public bool MarkSynced(DateTime nowUtc, long version)
    {
        if (Stale(version))
        {
            return false;
        }

        return Status switch
        {
            SyncStatus.Pending => Move(SyncStatus.Synced, null, nowUtc, version),
            SyncStatus.Synced => Move(SyncStatus.Synced, null, nowUtc, version),
            _ => false
        };
    }

    public bool MarkFailed(string reasonCode, DateTime nowUtc, long version = 0) =>
        !Stale(version) && Status is SyncStatus.Pending or SyncStatus.Failed && Move(SyncStatus.Failed, reasonCode, nowUtc, version);

    /// <summary>Failed to Pending (a retry has started).</summary>
    public bool Retry(DateTime nowUtc, long version = 0) =>
        !Stale(version) && Status == SyncStatus.Failed && Move(SyncStatus.Pending, null, nowUtc, version);

    /// <summary>Synced to Archived (the source closed or deleted the job).</summary>
    public bool Archive(DateTime nowUtc, long version) =>
        !Stale(version) && Status == SyncStatus.Synced && Move(SyncStatus.Archived, null, nowUtc, version);
}

/// <summary>Daily counters per partner (US-3.1.3-11). "Matched" and "viewed" have no source events yet (Q-02) and stay 0.</summary>
public sealed class IntegrationUsageDaily : Entity<Guid>
{
    private IntegrationUsageDaily()
    {
    }

    public Guid PartnerId { get; private set; }
    public DateOnly Day { get; private set; }
    public int Submitted { get; private set; }
    public int Matched { get; private set; }
    public int Viewed { get; private set; }

    public static IntegrationUsageDaily For(Guid partnerId, DateOnly day) =>
        new() { Id = Guid.NewGuid(), PartnerId = partnerId, Day = day };

    public void CountSubmission() => Submitted++;
}

/// <summary>Validated inclusive date window of a usage query. End before start is E-TPJPRI-INVALID-FIELD (AL.Usage.INVALID_DATE_RANGE).</summary>
public sealed class UsageWindow : ValueObject
{
    public const int MaxDays = 366;

    private UsageWindow(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    public DateOnly From { get; }
    public DateOnly To { get; }

    public static UsageWindow Create(DateOnly from, DateOnly to)
    {
        Guard.Ensure(to >= from, AuditRuleCodes.InvalidDateRange, "The end date must not be before the start date.", AuditErrorCodes.PartnerInvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(to.DayNumber - from.DayNumber <= MaxDays, AuditRuleCodes.InvalidDateRange, "The range may not exceed the 12-month live window.",
            AuditErrorCodes.PartnerInvalidField, BusinessRuleKind.InvalidInput);
        return new UsageWindow(from, to);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return From;
        yield return To;
    }
}

/// <summary>One old status to new status row of a posting (US-3.2.4-02). The source message id makes redelivery harmless.</summary>
public sealed class JobStatusHistoryEntry : Entity<Guid>
{
    private JobStatusHistoryEntry()
    {
    }

    public Guid SourceMessageId { get; private set; }
    public Guid JobPostingId { get; private set; }
    public Guid EmployerId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    public static JobStatusHistoryEntry Record(Guid sourceMessageId, Guid jobPostingId, Guid employerId, string? fromStatus, string toStatus, string? reason,
        DateTime changedAtUtc)
    {
        Guard.Ensure(jobPostingId != Guid.Empty && employerId != Guid.Empty && !string.IsNullOrWhiteSpace(toStatus), AuditRuleCodes.InvalidEntry,
            "A status change needs a posting, an employer and the new status.");
        return new JobStatusHistoryEntry
        {
            Id = Guid.NewGuid(),
            SourceMessageId = sourceMessageId,
            JobPostingId = jobPostingId,
            EmployerId = employerId,
            FromStatus = string.IsNullOrWhiteSpace(fromStatus) ? null : fromStatus,
            ToStatus = toStatus,
            Reason = reason,
            ChangedAtUtc = changedAtUtc
        };
    }
}

/// <summary>A user's notification with its delivery status; also the source of the e-mail and SMS logs. The recipient is always masked (THR-038).</summary>
public sealed class NotificationLogEntry : Entity<Guid>
{
    private NotificationLogEntry()
    {
    }

    public Guid? RecipientId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string MaskedRecipient { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime SentAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static NotificationLogEntry Record(Guid notificationId, Guid? recipientId, string channel, string category, string maskedRecipient, string? subject,
        string status, DateTime sentAtUtc)
    {
        Guard.Ensure(notificationId != Guid.Empty && !string.IsNullOrWhiteSpace(channel), AuditRuleCodes.InvalidEntry, "A notification needs an id and a channel.");
        // A masked recipient contains no full address or number; anything that still looks like one is refused.
        Guard.Ensure(!maskedRecipient.Contains('@') || maskedRecipient.Contains('*'), AuditRuleCodes.PiiInDetails, "The recipient must be masked.",
            AuditErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        return new NotificationLogEntry
        {
            Id = notificationId,
            RecipientId = recipientId,
            Channel = channel,
            Category = category,
            MaskedRecipient = maskedRecipient,
            Subject = subject,
            Status = status,
            SentAtUtc = sentAtUtc,
            UpdatedAtUtc = sentAtUtc
        };
    }

    public void UpdateStatus(string status, DateTime atUtc)
    {
        if (atUtc < UpdatedAtUtc)
        {
            return; // out-of-order: a newer status is already stored
        }

        Status = status;
        UpdatedAtUtc = atUtc;
    }
}

public sealed class DashboardPosting
{
    private DashboardPosting()
    {
    }

    public Guid JobPostingId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    internal static DashboardPosting Create(Guid jobPostingId, string title, string status, DateTime atUtc) =>
        new() { JobPostingId = jobPostingId, Title = title, Status = status, UpdatedAtUtc = atUtc };

    internal void Update(string? title, string status, DateTime atUtc)
    {
        if (atUtc < UpdatedAtUtc)
        {
            return;
        }

        Title = string.IsNullOrWhiteSpace(title) ? Title : title;
        Status = status;
        UpdatedAtUtc = atUtc;
    }
}

/// <summary>One-page recruitment view of an employer (US-3.1.2-07): postings, shortlists and key metrics. Read-only for the owner; this BC never mutates the source data.</summary>
public sealed class EmployerDashboard : Entity<Guid>
{
    private readonly List<DashboardPosting> _postings = new();

    private EmployerDashboard()
    {
    }

    public IReadOnlyCollection<DashboardPosting> Postings => _postings;
    public bool RegistrationApproved { get; private set; }
    public int ShortlistCount { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static EmployerDashboard Open(Guid employerId, DateTime nowUtc)
    {
        Guard.Ensure(employerId != Guid.Empty, AuditRuleCodes.InvalidEntry, "An employer id is required.");
        return new EmployerDashboard { Id = employerId, UpdatedAtUtc = nowUtc };
    }

    public void MarkRegistrationApproved(DateTime nowUtc)
    {
        RegistrationApproved = true;
        UpdatedAtUtc = nowUtc > UpdatedAtUtc ? nowUtc : UpdatedAtUtc;
    }

    public void TrackPosting(Guid jobPostingId, string? title, string status, DateTime atUtc)
    {
        var existing = _postings.FirstOrDefault(p => p.JobPostingId == jobPostingId);
        if (existing is null)
        {
            _postings.Add(DashboardPosting.Create(jobPostingId, title ?? string.Empty, status, atUtc));
        }
        else
        {
            existing.Update(title, status, atUtc);
        }

        UpdatedAtUtc = atUtc > UpdatedAtUtc ? atUtc : UpdatedAtUtc;
    }

    public void CountShortlist(DateTime atUtc)
    {
        ShortlistCount++;
        UpdatedAtUtc = atUtc > UpdatedAtUtc ? atUtc : UpdatedAtUtc;
    }
}

/// <summary>Insight about a candidate for one posting, projected from the BC-11 event. Fields the candidate withholds are reported "unavailable" (CandidateInsightPolicy).</summary>
public sealed class CandidateInsightRecord : Entity<Guid>
{
    private CandidateInsightRecord()
    {
    }

    public Guid JobPostingId { get; private set; }
    public Guid EmployerId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string? Availability { get; private set; }
    public decimal? ExpectedSalary { get; private set; }
    public decimal? FitScore { get; private set; }
    public string WithheldFieldsCsv { get; private set; } = string.Empty;
    public DateTime ComputedAtUtc { get; private set; }

    public IReadOnlyCollection<string> WithheldFields =>
        WithheldFieldsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static CandidateInsightRecord Compute(Guid insightId, Guid jobPostingId, Guid employerId, Guid candidateProfileId, string? availability,
        decimal? expectedSalary, decimal? fitScore, IEnumerable<string> withheld, DateTime computedAtUtc)
    {
        Guard.Ensure(jobPostingId != Guid.Empty && employerId != Guid.Empty && candidateProfileId != Guid.Empty, AuditRuleCodes.InvalidEntry,
            "An insight needs a posting, an employer and a candidate.");
        return new CandidateInsightRecord
        {
            Id = insightId,
            JobPostingId = jobPostingId,
            EmployerId = employerId,
            CandidateProfileId = candidateProfileId,
            Availability = availability,
            ExpectedSalary = expectedSalary,
            FitScore = fitScore,
            WithheldFieldsCsv = string.Join(',', withheld),
            ComputedAtUtc = computedAtUtc
        };
    }

    public void Refresh(string? availability, decimal? expectedSalary, decimal? fitScore, IEnumerable<string> withheld, DateTime computedAtUtc)
    {
        if (computedAtUtc < ComputedAtUtc)
        {
            return;
        }

        Availability = availability;
        ExpectedSalary = expectedSalary;
        FitScore = fitScore;
        WithheldFieldsCsv = string.Join(',', withheld);
        ComputedAtUtc = computedAtUtc;
    }
}
