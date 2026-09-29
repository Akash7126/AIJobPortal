using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

public sealed record RetireApiVersionCommand(string Version) : AdminCommand<Unit>;
