namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record IndustryAnalyticsDto(DateOnly From, DateOnly To, bool InsufficientData, bool SupplyDataAvailable, IReadOnlyList<IndustryRowDto> Rows);
