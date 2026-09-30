using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>US-3.3.1-06: the employer's reverse-matched candidates of an owned posting (empty is not an error).</summary>
public sealed record GetReverseMatchesQuery(Guid JobPostingId, int Page = 1, int PageSize = 20) : EmployerRequest, IQuery<PagedResult<MatchedCandidateDto>>;
