namespace JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

public sealed record ApiDocumentationView(string Version, string Content, bool Deprecated, DateTime? SunsetAtUtc);
