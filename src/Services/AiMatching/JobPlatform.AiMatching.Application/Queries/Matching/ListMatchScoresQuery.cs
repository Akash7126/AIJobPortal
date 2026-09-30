using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>Internal (BC-11): stored scores of a posting with breakdown.</summary>
public sealed record ListMatchScoresQuery(Guid JobPostingId, decimal? MinScore = null, int Page = 1, int PageSize = 50) : ServiceRequest, IQuery<MatchScoreListDto>;
