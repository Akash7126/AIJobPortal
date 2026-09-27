using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

/// <summary>Inbox-fed replica of a BC-04 profile: identity mapping (account to profile) and standing only. No profile content is copied here.</summary>
public sealed class KnownProfile : Entity<Guid>
{
    private KnownProfile()
    {
    }

    public Guid OwnerAccountId { get; private set; }
    public KnownStanding Standing { get; private set; }

    /// <summary>Highest BC-04 aggregate version seen: older (out-of-order) events are ignored.</summary>
    public long LastEventVersion { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static KnownProfile Create(Guid profileId, Guid ownerAccountId, long eventVersion, DateTime nowUtc) =>
        new() { Id = profileId, OwnerAccountId = ownerAccountId, Standing = KnownStanding.Active, LastEventVersion = eventVersion, UpdatedAtUtc = nowUtc };

    /// <summary>Applies a newer event; returns false when the event is stale (already seen or older).</summary>
    public bool Touch(long eventVersion, DateTime nowUtc)
    {
        if (eventVersion <= LastEventVersion)
        {
            return false;
        }

        LastEventVersion = eventVersion;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void Deactivate(DateTime nowUtc)
    {
        Standing = KnownStanding.Deactivated;
        UpdatedAtUtc = nowUtc;
    }
}

/// <summary>Inbox-fed replica of a BC-09 posting: owner, status and title. Ownership checks and the "only active postings" rule read this.</summary>
public sealed class KnownPosting : Entity<Guid>
{
    private KnownPosting()
    {
    }

    public Guid EmployerAccountId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public bool Suspended { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public long LastEventVersion { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Only active, non-suspended postings are scored and recommended (INV-05).</summary>
    public bool IsActive => !Suspended && string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);

    public static KnownPosting Create(Guid postingId, Guid employerAccountId, string status, string title, long eventVersion, DateTime nowUtc) =>
        new() { Id = postingId, EmployerAccountId = employerAccountId, Status = status, Title = Trim(title), LastEventVersion = eventVersion, UpdatedAtUtc = nowUtc };

    /// <summary>Applies a status change carried by an event; stale versions are ignored. Returns true when the state changed.</summary>
    public bool ApplyStatus(string status, long eventVersion, DateTime nowUtc)
    {
        if (eventVersion <= LastEventVersion)
        {
            return false;
        }

        Status = status;
        LastEventVersion = eventVersion;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void Suspend(DateTime nowUtc)
    {
        Suspended = true;
        UpdatedAtUtc = nowUtc;
    }

    private static string Trim(string title) => title.Length > 300 ? title[..300] : title;
}

public enum WorkItemKind
{
    RefreshProfileEmbedding,
    MatchesForPosting,
    MatchesForProfile,
    ComputeShortlist,
    ComputeRecommendation,
    Restandardize
}

public enum WorkItemStatus
{
    Pending,
    Done,
    Failed
}

/// <summary>A unit of long-running work for the background worker (handover 3.9: batch computations run outside the request and the inbox transaction).</summary>
public sealed class MatchingWorkItem : Entity<Guid>
{
    private MatchingWorkItem()
    {
    }

    public WorkItemKind Kind { get; private set; }
    public Guid EntityId { get; private set; }
    public WorkItemStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public static MatchingWorkItem Enqueue(WorkItemKind kind, Guid entityId, DateTime nowUtc) =>
        new() { Id = Guid.NewGuid(), Kind = kind, EntityId = entityId, Status = WorkItemStatus.Pending, NextAttemptUtc = nowUtc, CreatedAtUtc = nowUtc };

    public void MarkDone(DateTime nowUtc)
    {
        Status = WorkItemStatus.Done;
        ProcessedAtUtc = nowUtc;
        LastError = null;
    }

    /// <summary>Records a failure; after <paramref name="maxAttempts"/> the item is Failed, otherwise it is retried later.</summary>
    public void MarkFailed(string error, DateTime nowUtc, int maxAttempts, TimeSpan backoff)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
        if (Attempts >= maxAttempts)
        {
            Status = WorkItemStatus.Failed;
            ProcessedAtUtc = nowUtc;
        }
        else
        {
            NextAttemptUtc = nowUtc + backoff;
        }
    }
}
