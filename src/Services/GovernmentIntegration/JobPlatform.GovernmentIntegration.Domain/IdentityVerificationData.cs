using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum IdentityStatus
{
    Requested,
    Verified,
    Unverified,
    SystemUnavailable
}

/// <summary>PII, encrypted at rest, never logged (handover section 3.4).</summary>
public sealed class IdentityClaim : ValueObject
{
    public IdentityClaim(string nationalIdReference, string fullName, DateOnly dateOfBirth)
    {
        NationalIdReference = nationalIdReference;
        FullName = fullName;
        DateOfBirth = dateOfBirth;
    }

    public string NationalIdReference { get; }
    public string FullName { get; }
    public DateOnly DateOfBirth { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return NationalIdReference;
        yield return FullName;
        yield return DateOfBirth;
    }
}

/// <summary>Reason an identity check resolved to Unverified. AMBIGUOUS covers handover Q-05 (several candidate matches ⇒ manual follow-up).</summary>
public static class UnverifiedReasons
{
    public const string NoMatch = "NO_MATCH";
    public const string Ambiguous = "AMBIGUOUS";
}

/// <summary>AGG-28: result of checking identity details against a government ID system (handover section 3.4, story US-3.4.2-04).</summary>
public sealed class IdentityVerificationData : AggregateRoot<Guid>
{
    public const int MaxAttempts = 3;

    private IdentityVerificationData()
    {
    }

    public Subject Subject { get; private set; } = default!;
    public IdentityClaim IdentityClaim { get; private set; } = default!;
    public IdentityStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? UnverifiedReason { get; private set; }
    public DateTime RetentionExpiresAtUtc { get; private set; }

    public bool AttemptsExhausted => AttemptCount >= MaxAttempts;

    public static IdentityVerificationData Request(Guid id, Subject subject, IdentityClaim claim, DateTime nowUtc, int retentionMonths = 12)
    {
        Check(new BusinessRule(RuleCodes.GovDataSubjectRequired, "An import must reference the subject it verifies.",
            subject.SubjectId == Guid.Empty, ErrorCodes.SubjectRequired, BusinessRuleKind.BusinessRule));

        return new IdentityVerificationData
        {
            Id = id,
            Subject = subject,
            IdentityClaim = claim,
            Status = IdentityStatus.Requested,
            RetentionExpiresAtUtc = nowUtc.AddMonths(retentionMonths)
        };
    }

    public void RecordAttempt() => AttemptCount++;

    /// <summary>INV-10: an import must resolve to exactly one subject. Only called when the source resolved a single unambiguous match.</summary>
    public void MarkVerified(DateTime nowUtc)
    {
        Check(NotRequestedRule());
        Status = IdentityStatus.Verified;
        Raise(new IdentityVerificationDataImportedDomainEvent(Id, Subject.SubjectId, "Verified", nowUtc));
    }

    /// <summary>Handover Q-05 (proposed): several candidates ⇒ Unverified(AMBIGUOUS) with manual follow-up, rather than picking one.</summary>
    public void MarkUnverified(string reason, DateTime nowUtc)
    {
        Check(NotRequestedRule());
        Status = IdentityStatus.Unverified;
        UnverifiedReason = reason;
        Raise(new IdentityVerificationDataImportedDomainEvent(Id, Subject.SubjectId, "Unverified", nowUtc));
    }

    public void RecordSystemUnavailable() => Status = IdentityStatus.SystemUnavailable;

    private IBusinessRule NotRequestedRule() =>
        new BusinessRule("GI.Identity.NOT_REQUESTED", "Only a requested import can be resolved.", Status != IdentityStatus.Requested,
            ErrorCodes.NotRequested, BusinessRuleKind.Conflict);
}
