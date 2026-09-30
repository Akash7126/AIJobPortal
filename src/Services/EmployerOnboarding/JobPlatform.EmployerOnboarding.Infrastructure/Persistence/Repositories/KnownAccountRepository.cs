using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence.Repositories;

internal sealed class KnownAccountRepository(EmployerOnboardingDbContext db) : IKnownAccountRepository
{
    public Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        db.KnownAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownAccount account) => db.KnownAccounts.Add(account);
}
