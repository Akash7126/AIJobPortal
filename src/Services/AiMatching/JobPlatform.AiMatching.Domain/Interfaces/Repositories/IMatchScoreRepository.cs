namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IMatchScoreRepository
{
    Task<MatchScore?> GetPairAsync(Guid profileId, Guid jobPostingId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchScore>> ListByPostingAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchScore>> ListByProfileAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>Removes the stored scores of a posting that is no longer active (WithdrawPosting).</summary>
    Task<int> RemoveByPostingAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(MatchScore score);
}
