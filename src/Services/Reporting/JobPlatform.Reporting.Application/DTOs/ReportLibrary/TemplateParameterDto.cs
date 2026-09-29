namespace JobPlatform.Reporting.Application.DTOs.ReportLibrary;

public sealed record TemplateParameterDto(string Name, string Type, string? Min, string? Max, string? Default, IReadOnlyList<string>? Options);
