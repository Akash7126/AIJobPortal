namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface ICandidateShortlistRepository
{
    Task<CandidateShortlist?> GetAsync(Guid id, CancellationToken ct = default);

    void Add(CandidateShortlist shortlist);
}
