namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface ICompanyProfilePageRepository
{
    Task<CompanyProfilePage?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(CompanyProfilePage page);
}
