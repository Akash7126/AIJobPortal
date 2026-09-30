using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Application.Queries.Exports;
using JobPlatform.Reporting.Application.Services.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Exports;

internal sealed class DownloadReportExportHandler : IQueryHandler<DownloadReportExportQuery, ExportFileDto>
{
    private readonly IReportExportRepository _exports;
    private readonly ICurrentUser _user;
    private readonly ExportService _exportService;

    public DownloadReportExportHandler(IReportExportRepository exports, ICurrentUser user, ExportService exportService)
    {
        _exports = exports;
        _user = user;
        _exportService = exportService;
    }

    public async Task<Result<ExportFileDto>> Handle(DownloadReportExportQuery request, CancellationToken ct)
    {
        if (await _exportService.DeniedAsync(nameof(DownloadReportExportQuery), ct) is { } denied)
        {
            return denied;
        }

        var export = await _exports.GetAsync(request.Id, ct);
        if (export is null || export.RequestedBy != _user.UserId)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.");
        }

        return await _exportService.FileOf(export, ct);
    }
}
