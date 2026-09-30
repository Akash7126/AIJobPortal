namespace JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;

public interface ICompanyMediaRepository
{
    Task<CompanyMediaAndDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CompanyMediaAndDocument?> GetByHashAsync(Guid employerAccountId, string sha256, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyMediaAndDocument>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(CompanyMediaAndDocument media);
}
