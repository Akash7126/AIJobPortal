using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>US-3.3.1-01: score with the six-criterion breakdown for one posting; computed on demand when it was below the storage margin.</summary>
public sealed record GetMatchScoreQuery(Guid JobPostingId) : SeekerRequest, IQuery<MatchScoreDetailDto>;
