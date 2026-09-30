using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class CompanyProfilePageRepository(HelpContentDbContext db) : ICompanyProfilePageRepository
{
    public Task<CompanyProfilePage?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.CompanyProfilePages.FirstOrDefaultAsync(p => p.EmployerAccountId == employerAccountId, ct);

    public void Add(CompanyProfilePage page) => db.CompanyProfilePages.Add(page);
}
