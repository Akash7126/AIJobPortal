using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.ReportRuns;

/// <summary>Rendered view of a report result: tabular, chart spec or visualisation spec (US-3.5.4-03).</summary>
public sealed record ReportViewDto(string Format, string Title, ReportTable? Table, IReadOnlyList<string>? Labels, IReadOnlyList<ChartSeriesDto>? Series, string? ChartType,
    IReadOnlyList<KeyValuePair<string, decimal?>>? Highlights);
