using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

public sealed record ReleaseApiVersionCommand(string Version) : AdminCommand<ApiVersionView>;
