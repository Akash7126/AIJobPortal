using JobPlatform.AiMatching.Application.DTOs.Configuration;
using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Application.DTOs.Parsing;
using JobPlatform.AiMatching.Application.DTOs.Recommendations;
using JobPlatform.AiMatching.Application.DTOs.Shortlists;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AiMatching.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): projections over the stored scores and replicas, never aggregates.</summary>
public interface IMatchReadStore
{
    /// <summary>Stored scores of a profile for active postings, best first.</summary>
    Task<PagedResult<MatchedJobDto>> ListJobRankingAsync(Guid profileId, decimal minScore, PageRequest page, CancellationToken ct = default);

    /// <summary>Stored scores of a posting for active profiles, best first (ties: lowest profile id).</summary>
    Task<PagedResult<MatchedCandidateDto>> ListCandidatesAsync(Guid jobPostingId, decimal minScore, PageRequest page, CancellationToken ct = default);

    Task<PagedResult<MatchScoreDto>> ListScoresWithBreakdownAsync(Guid jobPostingId, decimal minScore, PageRequest page, CancellationToken ct = default);

    Task<ParsedProfileDataDto?> GetParsedProfileDataAsync(Guid ownerAccountId, CancellationToken ct = default);

    Task<ResumeParsedDataDto?> GetResumeParsedDataAsync(Guid id, CancellationToken ct = default);

    Task<ShortlistDto?> GetShortlistAsync(Guid id, CancellationToken ct = default);

    Task<JobRecommendationDto?> GetLatestRecommendationAsync(Guid profileId, CancellationToken ct = default);

    Task<MatchingConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default);
}
