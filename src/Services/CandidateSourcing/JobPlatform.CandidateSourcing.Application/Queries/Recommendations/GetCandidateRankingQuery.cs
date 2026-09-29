using JobPlatform.CandidateSourcing.Application.DTOs.Recommendations;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.CandidateSourcing.Application.Queries.Recommendations;

/// <summary>US-3.3.3-02: the recommendation with a stable rank position (CS.Ranking.TIE_BREAK_LOWEST_ID) and strengths/gaps highlighted.</summary>
public sealed record GetCandidateRankingQuery(Guid JobPostingId, int Page, int PageSize) : EmployerQuery<PagedResult<CandidateRankingItemView>>;
