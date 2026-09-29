using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Matching;

public sealed record ComputeMatchesForPostingCommand(Guid JobPostingId) : ICommand<int>;
