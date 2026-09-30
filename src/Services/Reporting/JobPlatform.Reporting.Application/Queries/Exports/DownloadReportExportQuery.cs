using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Exports;

public sealed record DownloadReportExportQuery(Guid Id) : CustomRequest, IQuery<ExportFileDto>;
