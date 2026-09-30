using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;

public interface IApiCredentialRepository
{
    Task<ApiCredential?> GetByIdAsync(ApiCredentialId id, CancellationToken ct = default);

    Task<ApiCredential?> GetByKeyIdAsync(string keyId, CancellationToken ct = default);

    /// <summary>The credential in status Active for the partner (even when past its expiry, until revoked).</summary>
    Task<ApiCredential?> GetActiveByPartnerAsync(AccountId partnerAccountId, CancellationToken ct = default);

    void Add(ApiCredential credential);
}
