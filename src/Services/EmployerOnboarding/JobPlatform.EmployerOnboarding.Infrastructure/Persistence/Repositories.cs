using JobPlatform.EmployerOnboarding.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence;

internal sealed class EmployerRegistrationRepository(EmployerOnboardingDbContext db) : IEmployerRegistrationRepository
{
    public Task<EmployerRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.EmployerRegistrations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<EmployerRegistration?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerRegistrations.FirstOrDefaultAsync(r => r.EmployerAccountId == employerAccountId, ct);

    public void Add(EmployerRegistration registration) => db.EmployerRegistrations.Add(registration);
}

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

internal sealed class EmployerStandingRepository(EmployerOnboardingDbContext db) : IEmployerStandingRepository
{
    public Task<EmployerStanding?> GetAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerStandings.Include(s => s.BadgeAudit).FirstOrDefaultAsync(s => s.EmployerAccountId == employerAccountId, ct);

    public void Add(EmployerStanding standing) => db.EmployerStandings.Add(standing);
}

internal sealed class KnownAccountRepository(EmployerOnboardingDbContext db) : IKnownAccountRepository
{
    public Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        db.KnownAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownAccount account) => db.KnownAccounts.Add(account);
}
