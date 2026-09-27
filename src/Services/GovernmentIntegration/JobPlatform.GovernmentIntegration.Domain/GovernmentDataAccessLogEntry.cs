namespace JobPlatform.GovernmentIntegration.Domain;

/// <summary>US-3.4.2-06 AC-01 / US-2.5-03 AC-02: every access decision (allow and deny) is written here. Append-only, not an aggregate root -
/// no state transitions, just a fact record. Retention 12 months per handover section 8.1.</summary>
public sealed class GovernmentDataAccessLogEntry
{
    private GovernmentDataAccessLogEntry()
    {
    }

    public GovernmentDataAccessLogEntry(Guid id, DateTime occurredAtUtc, string component, AccessPurpose purpose, string subjectRef,
        string decision, string? errorCode)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc;
        Component = component;
        Purpose = purpose;
        SubjectRef = subjectRef;
        Decision = decision;
        ErrorCode = errorCode;
    }

    public Guid Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Component { get; private set; } = string.Empty;
    public AccessPurpose Purpose { get; private set; }
    public string SubjectRef { get; private set; } = string.Empty;

    /// <summary>"Allow" or "Deny".</summary>
    public string Decision { get; private set; } = string.Empty;

    public string? ErrorCode { get; private set; }
}
