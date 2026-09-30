namespace JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;

public interface IEmployerStandingRepository
{
    Task<EmployerStanding?> GetAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(EmployerStanding standing);
}
