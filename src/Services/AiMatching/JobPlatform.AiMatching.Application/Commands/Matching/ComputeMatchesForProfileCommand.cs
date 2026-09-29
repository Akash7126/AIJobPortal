using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Matching;

public sealed record ComputeMatchesForProfileCommand(Guid ProfileId) : ICommand<int>;
