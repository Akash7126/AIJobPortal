using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Application.Queries.Exports;
using JobPlatform.Reporting.Application.Services.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Exports;

internal sealed class DownloadSharedReportHandler : IQueryHandler<DownloadSharedReportQuery, ExportFileDto>
{
    private readonly IReportExportRepository _exports;
    private readonly IReportLinkSigner _signer;
    private readonly ExportService _exportService;

    public DownloadSharedReportHandler(IReportExportRepository exports, IReportLinkSigner signer, ExportService exportService)
    {
        _exports = exports;
        _signer = signer;
        _exportService = exportService;
    }

    public async Task<Result<ExportFileDto>> Handle(DownloadSharedReportQuery request, CancellationToken ct)
    {
        if (!_signer.Verify(request.Id, request.Expires, request.Signature, _exportService.Now))
        {
            return Error.Forbidden(ReportingErrorCodes.LinkInvalid, "The link is invalid or has expired.");
        }

        var export = await _exports.GetAsync(request.Id, ct);
        return export is null ? Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.") : await _exportService.FileOf(export, ct);
    }
}
