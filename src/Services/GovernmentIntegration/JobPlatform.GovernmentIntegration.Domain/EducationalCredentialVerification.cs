using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum EducationalStatus
{
    Requested,
    Verified,
    Unverified,
    InstitutionUnavailable
}

/// <summary>The education entry being checked against an institution database (handover section 3.3).</summary>
public sealed class Credential : ValueObject
{
    public Credential(string institution, string credentialName, int year)
    {
        Institution = institution;
        CredentialName = credentialName;
        Year = year;
    }

    public string Institution { get; }
    public string CredentialName { get; }
    public int Year { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Institution;
        yield return CredentialName;
        yield return Year;
    }
}

/// <summary>AGG-27: result of checking an education entry with an institution database (handover section 3.3, story US-3.4.2-03).</summary>
public sealed class EducationalCredentialVerification : AggregateRoot<Guid>
{
    public const int MaxAttempts = 3;

    private EducationalCredentialVerification()
    {
    }

    public Guid SubjectId { get; private set; }
    public Credential Credential { get; private set; } = default!;
    public EducationalStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime RetentionExpiresAtUtc { get; private set; }

    public bool AttemptsExhausted => AttemptCount >= MaxAttempts;

    /// <summary>INV-08: the import must reference the subject it verifies.</summary>
    public static EducationalCredentialVerification Request(Guid id, Guid subjectId, Credential credential, DateTime nowUtc, int retentionMonths = 12)
    {
        Check(new BusinessRule("GI.Credential.CREDENTIAL_SUBJECT_REQUIRED", "An import must reference the subject it verifies.",
            subjectId == Guid.Empty, ErrorCodes.SubjectRequired, BusinessRuleKind.BusinessRule));

        return new EducationalCredentialVerification
        {
            Id = id,
            SubjectId = subjectId,
            Credential = credential,
            Status = EducationalStatus.Requested,
            RetentionExpiresAtUtc = nowUtc.AddMonths(retentionMonths)
        };
    }

    public void RecordAttempt() => AttemptCount++;

    public void MarkVerified(DateTime nowUtc)
    {
        Check(NotRequestedRule());
        Status = EducationalStatus.Verified;
        Raise(new EducationalCredentialVerificationImportedDomainEvent(Id, SubjectId, "Verified", nowUtc));
    }

    /// <summary>INV-09: no match is not a failure - the record is kept as Unverified (retried later), never silently discarded.</summary>
    public void MarkUnverified(DateTime nowUtc)
    {
        Check(NotRequestedRule());
        Status = EducationalStatus.Unverified;
        Raise(new EducationalCredentialVerificationImportedDomainEvent(Id, SubjectId, "Unverified", nowUtc));
    }

    public void RecordInstitutionUnavailable() => Status = EducationalStatus.InstitutionUnavailable;

    private IBusinessRule NotRequestedRule() =>
        new BusinessRule("GI.Credential.NOT_REQUESTED", "Only a requested import can be resolved.", Status != EducationalStatus.Requested,
            ErrorCodes.NotRequested, BusinessRuleKind.Conflict);
}
