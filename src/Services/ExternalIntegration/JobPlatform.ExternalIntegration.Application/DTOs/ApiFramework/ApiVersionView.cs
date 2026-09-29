namespace JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;

public sealed record ApiVersionView(
    string Version, string Status, DateTime? DeprecatedAtUtc, DateTime? SunsetAtUtc, IReadOnlyList<string> AcceptedFormats);
