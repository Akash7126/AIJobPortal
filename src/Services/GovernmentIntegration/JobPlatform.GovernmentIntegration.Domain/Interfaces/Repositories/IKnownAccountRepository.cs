namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IKnownAccountRepository
{
    Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(KnownAccount account);
}
