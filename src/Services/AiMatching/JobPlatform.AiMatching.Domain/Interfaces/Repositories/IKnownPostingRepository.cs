namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IKnownPostingRepository
{
    Task<KnownPosting?> GetAsync(Guid postingId, CancellationToken ct = default);

    Task<IReadOnlyList<KnownPosting>> ListActiveAsync(int skip, int take, CancellationToken ct = default);

    void Add(KnownPosting posting);
}
