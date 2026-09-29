using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.ReportLibrary;

public sealed record ListSavedReportsQuery : CustomRequest, IQuery<IReadOnlyList<SavedReportDto>>;
