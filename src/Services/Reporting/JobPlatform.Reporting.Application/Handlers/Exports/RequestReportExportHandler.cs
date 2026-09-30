using JobPlatform.Reporting.Application.Commands.Exports;
using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Application.Services.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Exports;

internal sealed class RequestReportExportHandler : ICommandHandler<RequestReportExportCommand, ExportDto>
{
    private readonly IReportExportRepository _exports;
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly ICurrentUser _user;
    private readonly ExportService _exportService;

    public RequestReportExportHandler(IReportExportRepository exports, IReportTemplateRepository templates, ISavedReportRepository saved, ICurrentUser user, ExportService exportService)
    {
        _exports = exports;
        _templates = templates;
        _saved = saved;
        _user = user;
        _exportService = exportService;
    }

    public async Task<Result<ExportDto>> Handle(RequestReportExportCommand request, CancellationToken ct)
    {
        if (await _exportService.DeniedAsync(nameof(RequestReportExportCommand), ct) is { } denied)
        {
            return denied;
        }

        var kind = Enum.Parse<ReportRefKind>(request.RefKind, true);
        var format = Enum.Parse<ReportFormat>(request.Format, true);
        var parameters = request.Parameters ?? new Dictionary<string, string>();
        var exists = kind switch
        {
            ReportRefKind.Template => await _templates.GetAsync(request.RefId!.Value, ct) is not null,
            ReportRefKind.SavedReport => await _saved.GetAsync(request.RefId!.Value, ct) is { } s && s.OwnerId == _user.UserId,
            _ => true
        };
        if (!exists)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The referenced report was not found.");
        }

        var administrator = _user.UserId!.Value;
        var hash = ReportExport.HashParameters(kind, request.RefId, format, parameters);
        var running = await _exports.FindRunningAsync(administrator, hash, ct);
        if (running is not null)
        {
            return ExportService.ToDto(running, true);
        }

        var export = ReportExport.Request(administrator, kind, request.RefId, format, parameters, _exportService.Now);
        _exports.Add(export);
        return ExportService.ToDto(export, false);
    }
}
