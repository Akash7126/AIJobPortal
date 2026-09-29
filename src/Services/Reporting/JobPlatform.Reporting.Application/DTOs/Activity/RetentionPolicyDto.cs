namespace JobPlatform.Reporting.Application.DTOs.Activity;

public sealed record RetentionPolicyDto(int RetentionMonths, int LegalMinimumMonths, DateTime UpdatedAtUtc);
