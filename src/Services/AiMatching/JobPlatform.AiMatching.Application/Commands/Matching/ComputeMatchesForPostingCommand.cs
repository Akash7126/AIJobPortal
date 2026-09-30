using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Matching;

public sealed record ComputeMatchesForPostingCommand(Guid JobPostingId) : ICommand<int>;
