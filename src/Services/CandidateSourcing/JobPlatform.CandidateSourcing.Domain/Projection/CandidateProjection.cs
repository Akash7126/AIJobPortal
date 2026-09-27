using JobPlatform.CandidateSourcing.Domain.Privacy;

namespace JobPlatform.CandidateSourcing.Domain.Projection;

/// <summary>
/// Local, searchable replica of a candidate's disclosed fields (US-3.3.3-04 candidate database). Built by fetching BC-04's candidate-view API when
/// a ProfileCreated/ProfileUpdated event arrives (the events themselves are near id-only - handover gap Q-04), and updated by AccountSuspended
/// (deactivation) and its own privacy fields. Not an aggregate root: it has no invariants of its own, only the upstream data it mirrors.
/// </summary>
public sealed class CandidateProjection
{
    private CandidateProjection()
    {
        Skills = Array.Empty<string>();
    }

    public Guid ProfileId { get; private set; }

    /// <summary>BC-03 account id that owns the profile - correlates AccountSuspended (which carries the account id, not the profile id) to this row.</summary>
    public Guid OwnerAccountId { get; private set; }

    public CandidateVisibility Visibility { get; private set; }

    public bool EmployerVisibilityOptIn { get; private set; }

    public bool Deactivated { get; private set; }

    public IReadOnlyList<string> Skills { get; private set; }

    public string? EducationLevel { get; private set; }

    public decimal? YearsOfExperience { get; private set; }

    public string? LocationCode { get; private set; }

    public decimal? SalaryMin { get; private set; }

    public decimal? SalaryMax { get; private set; }

    public string? Availability { get; private set; }

    public long LastEventVersion { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static CandidateProjection Create(Guid profileId, Guid ownerAccountId) => new() { ProfileId = profileId, OwnerAccountId = ownerAccountId };

    /// <summary>Idempotent per event version: an out-of-order delivery (a lower version arriving after a higher one) is ignored.</summary>
    public void ApplyCandidateView(
        CandidateVisibility visibility, bool employerVisibilityOptIn, bool deactivated, IReadOnlyCollection<string> skills, string? educationLevel,
        decimal? yearsOfExperience, string? locationCode, decimal? salaryMin, decimal? salaryMax, string? availability, long eventVersion, DateTime nowUtc)
    {
        if (eventVersion <= LastEventVersion && LastEventVersion > 0)
        {
            return;
        }

        Visibility = visibility;
        EmployerVisibilityOptIn = employerVisibilityOptIn;
        Deactivated = deactivated;
        Skills = skills.ToArray();
        EducationLevel = educationLevel;
        YearsOfExperience = yearsOfExperience;
        LocationCode = locationCode;
        SalaryMin = salaryMin;
        SalaryMax = salaryMax;
        Availability = availability;
        LastEventVersion = eventVersion;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkDeactivated(DateTime nowUtc)
    {
        Deactivated = true;
        UpdatedAtUtc = nowUtc;
    }

    public CandidateSnapshot ToSnapshot(IReadOnlyCollection<string> disclosedFields) =>
        new(ProfileId, Visibility, EmployerVisibilityOptIn, Deactivated, disclosedFields);
}

/// <summary>Local replica of employers whose government verification was approved (BC-01's EmployerVerificationApproved), so the candidate-database
/// search gate does not depend on a live call for every search.</summary>
public sealed class VerifiedEmployer
{
    private VerifiedEmployer()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public DateTime VerifiedAtUtc { get; private set; }

    public static VerifiedEmployer Create(Guid employerAccountId, DateTime nowUtc) => new() { EmployerAccountId = employerAccountId, VerifiedAtUtc = nowUtc };
}
