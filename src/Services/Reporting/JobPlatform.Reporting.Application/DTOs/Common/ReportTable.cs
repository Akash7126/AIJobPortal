namespace JobPlatform.Reporting.Application.DTOs.Common;

public sealed record ReportTable(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows, bool Truncated = false, int SuppressedRows = 0);
