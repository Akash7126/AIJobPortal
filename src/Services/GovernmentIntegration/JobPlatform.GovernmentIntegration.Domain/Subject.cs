using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain;

/// <summary>Who a verification is about: a job seeker profile or an employer (handover section 2, "Subject").</summary>
public enum SubjectType
{
    JobSeekerProfile,
    Employer
}

/// <summary>The external authority a check is made against (handover section 2, "Source system"). Not every aggregate uses every value:
/// <see cref="GovernmentVerificationData"/> uses MoL/PEF/GovernmentDatabase; <see cref="GovernmentSourceConnection"/> uses all five.</summary>
public enum SourceSystem
{
    MoL,
    PEF,
    GovernmentDatabase,
    EducationalInstitution,
    GovernmentIdSystem
}

/// <summary>Declared reason a component wants government data (handover section 2, "Access purpose"; US-3.4.2-06).</summary>
public enum AccessPurpose
{
    EmployerVerification,
    CredentialVerification,
    IdentityVerification,
    Enrichment,
    Migration
}

/// <summary>The profile or employer a verification/import is about (handover sections 3.2-3.4).</summary>
public sealed class Subject : ValueObject
{
    public Subject(SubjectType subjectType, Guid subjectId)
    {
        SubjectType = subjectType;
        SubjectId = subjectId;
    }

    public SubjectType SubjectType { get; }

    public Guid SubjectId { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SubjectType;
        yield return SubjectId;
    }
}
