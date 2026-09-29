namespace JobPlatform.Reporting.Application.DTOs.Common;

public sealed record ReportDefinitionDto(string DataSource, IReadOnlyList<string> Fields, IReadOnlyList<ReportFilterDto> Filters, IReadOnlyList<string> GroupBy);
