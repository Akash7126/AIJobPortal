using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.ReportRuns;

/// <summary>Result of running a report. Fallback = Power BI was requested but the upstream timed out, so the built-in view is returned with the warning code.</summary>
public sealed record ReportResultDto(string Target, bool Fallback, string? WarningCode, string? PowerBiUrl, string Title, ReportTable Table);
