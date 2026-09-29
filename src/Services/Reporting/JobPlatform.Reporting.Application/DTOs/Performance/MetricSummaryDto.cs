namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record MetricSummaryDto(string Metric, decimal? Latest, decimal? Average, decimal? Max, long Samples);
