using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;

internal sealed class ApiCredentialRepository : IApiCredentialRepository
{
    private readonly IdentityDbContext _db;

    public ApiCredentialRepository(IdentityDbContext db) => _db = db;

    public Task<ApiCredential?> GetByIdAsync(ApiCredentialId id, CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<ApiCredential?> GetByKeyIdAsync(string keyId, CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.KeyId == keyId, ct);

    public Task<ApiCredential?> GetActiveByPartnerAsync(AccountId partnerAccountId, CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.PartnerAccountId == partnerAccountId && c.Status == ApiCredentialStatus.Active, ct);

    public void Add(ApiCredential credential) => _db.ApiCredentials.Add(credential);
}
