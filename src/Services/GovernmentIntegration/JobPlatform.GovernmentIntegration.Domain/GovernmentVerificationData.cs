using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum GovernmentDataStatus
{
    Requested,
    Verified,
    NoMatch,
    SourceUnavailable
}

/// <summary>One field returned by a government database and stored against a subject (handover section 3.2). The column holding a collection
/// of these is encrypted at the persistence layer (foundation section 8) - the value object itself carries no encryption concern.</summary>
public sealed class VerifiedField : ValueObject
{
    public VerifiedField(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string Value { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Value;
    }
}

/// <summary>AGG-26: a record imported from a government database used to verify/enrich a subject (handover section 3.2, story US-3.4.2-01).</summary>
public sealed class GovernmentVerificationData : AggregateRoot<Guid>
{
    /// <summary>A-02-004: 3 retries before the source is considered unavailable.</summary>
    public const int MaxAttempts = 3;

    private GovernmentVerificationData()
    {
    }

    public Subject Subject { get; private set; } = default!;
    public SourceSystem Source { get; private set; }
    public GovernmentDataStatus Status { get; private set; }
    public AccessPurpose Purpose { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? ImportedAtUtc { get; private set; }
    public DateTime RetentionExpiresAtUtc { get; private set; }

    /// <summary>A settable (rather than field-backed) collection so the whole set can be mapped as one encrypted JSON column
    /// (handover section 8.1) without EF needing a child table for a value object with no identity.</summary>
    public IReadOnlyCollection<VerifiedField> VerifiedFields { get; private set; } = Array.Empty<VerifiedField>();

    public bool AttemptsExhausted => AttemptCount >= MaxAttempts;

    /// <summary>INV-06: an import must reference the subject it verifies. INV-07 (access authorised) is checked by the application via
    /// <see cref="GovernmentDataAccessPolicy"/> before this factory is called (section 3.9).</summary>
    public static GovernmentVerificationData Request(Guid id, Subject subject, SourceSystem source, AccessPurpose purpose, DateTime nowUtc,
        int retentionMonths = 12)
    {
        Check(new BusinessRule(RuleCodes.GovDataSubjectRequired, "An import must reference the subject it verifies.",
            subject.SubjectId == Guid.Empty, ErrorCodes.SubjectRequired, BusinessRuleKind.BusinessRule));

        return new GovernmentVerificationData
        {
            Id = id,
            Subject = subject,
            Source = source,
            Purpose = purpose,
            Status = GovernmentDataStatus.Requested,
            RetentionExpiresAtUtc = nowUtc.AddMonths(retentionMonths)
        };
    }

    public void RecordAttempt() => AttemptCount++;

    /// <summary>Only from Requested. Sets Verified, stores the fields and publishes GovernmentVerificationDataImported (outcome Verified).</summary>
    public void RecordMatch(IEnumerable<VerifiedField> fields, DateTime nowUtc)
    {
        Check(NotRequestedRule());
        VerifiedFields = fields.ToList();
        Status = GovernmentDataStatus.Verified;
        ImportedAtUtc = nowUtc;
        Raise(new GovernmentVerificationDataImportedDomainEvent(Id, Subject.SubjectType.ToString(), Subject.SubjectId, "Verified", nowUtc));
    }

    /// <summary>Only from Requested. AC-03: no match is not a failure - it still publishes GovernmentVerificationDataImported (outcome NoMatch,
    /// proposed payload flag per handover section 3.2).</summary>
    public void RecordNoMatch(DateTime nowUtc)
    {
        Check(NotRequestedRule());
        Status = GovernmentDataStatus.NoMatch;
        ImportedAtUtc = nowUtc;
        Raise(new GovernmentVerificationDataImportedDomainEvent(Id, Subject.SubjectType.ToString(), Subject.SubjectId, "NoMatch", nowUtc));
    }

    /// <summary>After 3 retries x 30s (AC-02): SourceUnavailable, error E-GDI-UPSTREAM-TIMEOUT logged by the caller; can be re-requested.</summary>
    public void RecordSourceUnavailable() => Status = GovernmentDataStatus.SourceUnavailable;

    /// <summary>Retention/privacy control (US-2.5-03 AC-03): after RetentionExpiresAtUtc, delete/anonymise the verified fields.</summary>
    public void Purge() => VerifiedFields = Array.Empty<VerifiedField>();

    private IBusinessRule NotRequestedRule() =>
        new BusinessRule(RuleCodes.GovDataNotRequested, "Only a requested import can be resolved.", Status != GovernmentDataStatus.Requested,
            ErrorCodes.NotRequested, BusinessRuleKind.Conflict);
}
