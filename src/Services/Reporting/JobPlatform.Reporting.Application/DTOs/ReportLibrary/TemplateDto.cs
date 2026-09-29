namespace JobPlatform.Reporting.Application.DTOs.ReportLibrary;

public sealed record TemplateDto(Guid Id, string Name, string DataSource, IReadOnlyList<TemplateParameterDto> Parameters, int Revision, DateTime UpdatedAtUtc);
