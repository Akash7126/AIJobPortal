namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record SalaryAnalyticsDto(DateOnly From, DateOnly To, string By, bool InsufficientData, IReadOnlyList<SalaryRowDto> Rows);
