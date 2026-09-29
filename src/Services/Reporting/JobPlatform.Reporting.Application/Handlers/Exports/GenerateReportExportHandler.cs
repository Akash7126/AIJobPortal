using JobPlatform.Reporting.Application.Commands.Exports;
using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Application.Services.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Reporting.Application.Handlers.Exports;

internal sealed class GenerateReportExportHandler : ICommandHandler<GenerateReportExportCommand, ExportDto>
{
    private readonly IReportExportRepository _exports;
    private readonly ExportContentBuilder _content;
    private readonly ILogger<GenerateReportExportHandler> _logger;
    private readonly ExportService _exportService;

    public GenerateReportExportHandler(IReportExportRepository exports, ExportContentBuilder content, ILogger<GenerateReportExportHandler> logger, ExportService exportService)
    {
        _exports = exports;
        _content = content;
        _logger = logger;
        _exportService = exportService;
    }

    public async Task<Result<ExportDto>> Handle(GenerateReportExportCommand request, CancellationToken ct)
    {
        var export = await _exports.GetAsync(request.ExportId, ct);
        if (export is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.");
        }

        if (export.Status != ExportStatus.Queued)
        {
            return ExportService.ToDto(export, false);
        }

        export.StartGenerating();
        try
        {
            var file = await _content.BuildAsync(export.RefKind, export.RefId, export.Parameters, export.Format, ct);
            if (file is null)
            {
                export.Fail("The referenced report no longer exists.", _exportService.Now);
            }
            else
            {
                _exports.AddFile(ReportExportFile.For(export.Id, file.FileName, file.ContentType, file.Content, _exportService.Now));
                export.Complete($"/api/v1/admin/reports/exports/{export.Id}/file", _exportService.Now);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Export {ExportId} failed", export.Id);
            export.Fail(ex.Message, _exportService.Now);
        }

        return ExportService.ToDto(export, false);
    }
}
