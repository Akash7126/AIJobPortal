namespace JobPlatform.JobPosting.Application.Interfaces;

/// <summary>Port to BC-10: recommended jobs for a logged-in job seeker (handover section 6.2). Adapter chosen by MatchRanking:Provider.</summary>
public interface IMatchRankingProvider
{
    /// <summary>Empty when BC-10 is unavailable - the caller degrades to plain search results (handover section 6.2).</summary>
    Task<IReadOnlyList<MatchRankingItemView>> GetRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default);
}
