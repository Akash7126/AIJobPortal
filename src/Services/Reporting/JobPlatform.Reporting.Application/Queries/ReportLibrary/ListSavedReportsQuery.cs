using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.ReportLibrary;

public sealed record ListSavedReportsQuery : CustomRequest, IQuery<IReadOnlyList<SavedReportDto>>;
