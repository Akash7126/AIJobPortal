namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record PerformanceDashboardDto(SystemPerformanceDto Performance, UsagePatternsDto Usage, IReadOnlyList<AlertDto> RecentAlerts);
