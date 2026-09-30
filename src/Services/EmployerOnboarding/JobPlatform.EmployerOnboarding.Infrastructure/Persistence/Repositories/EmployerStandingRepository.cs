using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence.Repositories;

internal sealed class EmployerStandingRepository(EmployerOnboardingDbContext db) : IEmployerStandingRepository
{
    public Task<EmployerStanding?> GetAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.EmployerStandings.Include(s => s.BadgeAudit).FirstOrDefaultAsync(s => s.EmployerAccountId == employerAccountId, ct);

    public void Add(EmployerStanding standing) => db.EmployerStandings.Add(standing);
}
