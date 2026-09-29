using JobPlatform.CandidateSourcing.Application.DTOs.Recommendations;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.CandidateSourcing.Application.Queries.Recommendations;

/// <summary>US-3.3.3-01: candidates for one of the employer's active postings, scoring at or above the effective threshold. Empty is a normal
/// result (CS.Recommendation.EMPTY_IS_OK), never an error.</summary>
public sealed record GetCandidateRecommendationsQuery(Guid JobPostingId, int Page, int PageSize) : EmployerQuery<PagedResult<CandidateRecommendationItemView>>;
