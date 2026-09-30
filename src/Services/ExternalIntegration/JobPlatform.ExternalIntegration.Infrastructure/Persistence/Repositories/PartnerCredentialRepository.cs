using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class PartnerCredentialRepository(ExternalIntegrationDbContext db) : IPartnerCredentialRepository
{
    public Task<PartnerCredential?> GetByIdAsync(Guid apiCredentialId, CancellationToken ct = default) =>
        db.PartnerCredentials.FirstOrDefaultAsync(c => c.ApiCredentialId == apiCredentialId, ct);

    public Task<bool> HasActiveCredentialAsync(Guid accountId, DateTime nowUtc, CancellationToken ct = default) =>
        db.PartnerCredentials.AnyAsync(c => c.AccountId == accountId && c.ExpiresAtUtc > nowUtc, ct);

    public void Add(PartnerCredential credential) => db.PartnerCredentials.Add(credential);
}
