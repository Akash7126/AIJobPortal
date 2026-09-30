using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class KnownAccountRepository(JobSeekerProfileDbContext db) : IKnownAccountRepository
{
    public Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default) => db.KnownAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownAccount account) => db.KnownAccounts.Add(account);
}
