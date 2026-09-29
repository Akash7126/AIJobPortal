using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Application.Queries.Exports;
using JobPlatform.Reporting.Application.Services.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Exports;

internal sealed class GetReportExportHandler : IQueryHandler<GetReportExportQuery, ExportDto>
{
    private readonly IReportExportRepository _exports;
    private readonly ICurrentUser _user;
    private readonly ExportService _exportService;

    public GetReportExportHandler(IReportExportRepository exports, ICurrentUser user, ExportService exportService)
    {
        _exports = exports;
        _user = user;
        _exportService = exportService;
    }

    public async Task<Result<ExportDto>> Handle(GetReportExportQuery request, CancellationToken ct)
    {
        if (await _exportService.DeniedAsync(nameof(GetReportExportQuery), ct) is { } denied)
        {
            return denied;
        }

        var export = await _exports.GetAsync(request.Id, ct);
        return export is null || export.RequestedBy != _user.UserId
            ? Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.")
            : ExportService.ToDto(export, false);
    }
}
