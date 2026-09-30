using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Exports;

public sealed record GetReportExportQuery(Guid Id) : CustomRequest, IQuery<ExportDto>;
