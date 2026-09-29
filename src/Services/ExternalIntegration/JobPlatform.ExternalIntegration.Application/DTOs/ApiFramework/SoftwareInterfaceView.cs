namespace JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

public sealed record SoftwareInterfaceView(Guid Id, string Category, string Name, string Endpoint, bool Enabled);
