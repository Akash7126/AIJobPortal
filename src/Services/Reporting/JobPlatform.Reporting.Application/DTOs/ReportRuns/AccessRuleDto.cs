namespace JobPlatform.Reporting.Application.DTOs.ReportRuns;

public sealed record AccessRuleDto(string Role, IReadOnlyList<string> Categories, DateTime UpdatedAtUtc);
