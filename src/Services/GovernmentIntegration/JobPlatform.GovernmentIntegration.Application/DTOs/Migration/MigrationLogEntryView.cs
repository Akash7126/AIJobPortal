namespace JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

public sealed record MigrationLogEntryView(string Phase, string Outcome, string Message, DateTime AtUtc);
