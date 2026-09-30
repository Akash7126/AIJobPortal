namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface IPartnerCredentialRepository
{
    Task<PartnerCredential?> GetByIdAsync(Guid apiCredentialId, CancellationToken ct = default);

    Task<bool> HasActiveCredentialAsync(Guid accountId, DateTime nowUtc, CancellationToken ct = default);

    void Add(PartnerCredential credential);
}
