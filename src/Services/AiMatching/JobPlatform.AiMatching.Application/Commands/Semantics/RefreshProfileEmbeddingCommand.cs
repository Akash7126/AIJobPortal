using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Commands.Semantics;

public sealed record RefreshProfileEmbeddingCommand(Guid ProfileId) : ICommand;
