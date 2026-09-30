namespace JobPlatform.JobPosting.Application.Interfaces;

/// <summary>Port to BC-05: is this employer approved/verified to post (handover section 6.2, Q-02)? Adapter chosen by EmployerStanding:Provider.</summary>
public interface IEmployerStandingProvider
{
    /// <summary>Null when BC-05 could not be reached; the caller applies the fail-open policy of <see cref="EmployerEligibilityPolicy"/>.</summary>
    Task<bool?> IsApprovedAsync(Guid employerAccountId, CancellationToken ct = default);
}
