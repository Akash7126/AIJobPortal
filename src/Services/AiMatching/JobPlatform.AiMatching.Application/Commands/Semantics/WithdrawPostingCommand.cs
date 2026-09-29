using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Commands.Semantics;

public sealed record WithdrawPostingCommand(Guid JobPostingId) : ICommand;
