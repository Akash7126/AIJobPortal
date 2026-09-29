namespace JobPlatform.Reporting.Application.DTOs.Common;

/// <summary>Column-oriented result of a report run.</summary>
public sealed record ReportColumn(string Name, string Kind);
