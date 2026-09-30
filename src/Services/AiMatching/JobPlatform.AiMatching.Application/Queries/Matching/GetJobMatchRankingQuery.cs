using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>US-3.3.1-05/06: the caller's ranked jobs (profile from the token), below-threshold matches excluded.</summary>
public sealed record GetJobMatchRankingQuery(decimal? MinScore = null, int Page = 1, int PageSize = 20) : SeekerRequest, IQuery<PagedResult<MatchedJobDto>>;
