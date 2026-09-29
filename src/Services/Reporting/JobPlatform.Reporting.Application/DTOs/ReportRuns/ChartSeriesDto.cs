namespace JobPlatform.Reporting.Application.DTOs.ReportRuns;

public sealed record ChartSeriesDto(string Name, IReadOnlyList<decimal?> Values);
