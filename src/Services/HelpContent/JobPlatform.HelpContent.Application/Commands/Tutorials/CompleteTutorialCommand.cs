using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.Tutorials;

public sealed record CompleteTutorialCommand(Guid TutorialId) : AuthenticatedCommand<Unit>;
