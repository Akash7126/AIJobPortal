namespace JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface IEmployerRegistrationRepository
{
    Task<EmployerRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EmployerRegistration?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(EmployerRegistration registration);
}
