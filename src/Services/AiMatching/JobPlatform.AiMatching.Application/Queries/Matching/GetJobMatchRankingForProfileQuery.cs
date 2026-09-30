using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>Internal (BC-09 search): the ranking of an explicit profile.</summary>
public sealed record GetJobMatchRankingForProfileQuery(Guid ProfileId, int Page = 1, int PageSize = 20) : ServiceRequest, IQuery<MatchRankingDto>;
