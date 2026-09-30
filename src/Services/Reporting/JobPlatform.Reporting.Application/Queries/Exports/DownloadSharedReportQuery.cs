using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Exports;

/// <summary>Anonymous download through the signed, expiring link carried by ReportDistributionRequested.</summary>
public sealed record DownloadSharedReportQuery(Guid Id, long Expires, string Signature) : IQuery<ExportFileDto>;
