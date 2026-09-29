using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

public sealed record RegisterSoftwareInterfaceCommand(string Category, string Name, string Endpoint) : AdminCommand<SoftwareInterfaceView>;
