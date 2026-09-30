using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Matching;

/// <summary>Persists the score of a pair (worker / operations); the request pipeline wraps it in a transaction and publishes MatchScoreComputed.</summary>
public sealed record ComputeMatchScoreCommand(Guid ProfileId, Guid JobPostingId) : ICommand<MatchScoreDetailDto>;
