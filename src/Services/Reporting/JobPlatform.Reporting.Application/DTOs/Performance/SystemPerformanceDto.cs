namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record SystemPerformanceDto(DateOnly From, DateOnly To, bool NoTechnicalData, MatchingMetricsDto Matching, IReadOnlyList<MetricSummaryDto> Technical);
