using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.ReportLibrary;

public sealed record SavedReportDto(Guid Id, string Name, ReportDefinitionDto Definition, DateTime CreatedAtUtc, bool Archived);
