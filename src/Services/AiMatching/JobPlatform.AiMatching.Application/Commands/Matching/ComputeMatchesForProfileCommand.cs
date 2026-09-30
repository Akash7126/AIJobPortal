using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Matching;

public sealed record ComputeMatchesForProfileCommand(Guid ProfileId) : ICommand<int>;
