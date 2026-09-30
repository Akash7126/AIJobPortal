namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IKnownProfileRepository
{
    Task<KnownProfile?> GetAsync(Guid profileId, CancellationToken ct = default);

    Task<KnownProfile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    Task<IReadOnlyList<KnownProfile>> ListActiveAsync(int skip, int take, CancellationToken ct = default);

    void Add(KnownProfile profile);
}
