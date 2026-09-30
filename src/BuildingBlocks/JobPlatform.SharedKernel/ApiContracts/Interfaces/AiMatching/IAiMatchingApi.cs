using JobPlatform.SharedKernel.ApiContracts.AiMatching;

namespace JobPlatform.SharedKernel.ApiContracts.Interfaces.AiMatching;

/// <summary>
/// Synchronous contract of BC-10 AI Matching for other BCs (routes under /internal/v1, service token with scope identity.internal).
/// Consumers: BC-11 (match-scores), BC-09 (match-ranking), BC-04 (resume-parsed-data).
/// </summary>
public interface IAiMatchingApi
{
    Task<MatchScoreListDto> ListMatchScoresAsync(Guid jobPostingId, decimal? minScore, int page, int pageSize, CancellationToken ct = default);

    Task<MatchRankingDto> GetMatchRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default);

    Task<ResumeParsedDataDto?> GetResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default);
}
