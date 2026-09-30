using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence.Repositories;

internal sealed class CompanyMediaRepository(EmployerOnboardingDbContext db) : ICompanyMediaRepository
{
    public Task<CompanyMediaAndDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.CompanyMedia.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<CompanyMediaAndDocument?> GetByHashAsync(Guid employerAccountId, string sha256, CancellationToken ct = default) =>
        db.CompanyMedia.FirstOrDefaultAsync(m => m.EmployerAccountId == employerAccountId && m.File.Sha256 == sha256 && !m.IsRemoved, ct);

    public async Task<IReadOnlyList<CompanyMediaAndDocument>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        await db.CompanyMedia.Where(m => m.EmployerAccountId == employerAccountId && !m.IsRemoved).ToListAsync(ct);

    public void Add(CompanyMediaAndDocument media) => db.CompanyMedia.Add(media);
}
