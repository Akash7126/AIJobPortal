namespace JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

public sealed record MigrationPhaseView(string Name, string Status, string? TestOutcome);
