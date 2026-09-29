namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record MetricHistoryDto(string Metric, DateOnly From, DateOnly To, string Granularity, IReadOnlyList<HistoryPointDto> Points);
