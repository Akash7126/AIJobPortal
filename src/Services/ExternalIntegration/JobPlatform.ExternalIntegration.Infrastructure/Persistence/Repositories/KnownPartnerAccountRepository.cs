using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class KnownPartnerAccountRepository(ExternalIntegrationDbContext db) : IKnownPartnerAccountRepository
{
    public Task<KnownPartnerAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        db.KnownPartnerAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownPartnerAccount account) => db.KnownPartnerAccounts.Add(account);
}
