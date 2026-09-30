namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface IEmployerVerificationRepository
{
    Task<EmployerVerification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The verification that is still Pending or PendingManualReview for this employer, if any (one-active-per-employer, section 8.1).</summary>
    Task<EmployerVerification?> GetActiveByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<bool> ExistsActiveAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(EmployerVerification verification);
}
