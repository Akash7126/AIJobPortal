using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.CandidateSourcing.Application.Queries.Search;

/// <summary>US-3.3.3-04: full-text-ish filter search over the local candidate projection. Verified employers only (CS.Search.NOT_VERIFIED).</summary>
public sealed record SearchCandidateDatabaseQuery(CandidateSearchCriteria Criteria, int Page, int PageSize) : EmployerQuery<PagedResult<CandidateSearchResultItemView>>;
