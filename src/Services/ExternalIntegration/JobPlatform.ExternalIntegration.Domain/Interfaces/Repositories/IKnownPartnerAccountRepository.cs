namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface IKnownPartnerAccountRepository
{
    Task<KnownPartnerAccount?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(KnownPartnerAccount account);
}
