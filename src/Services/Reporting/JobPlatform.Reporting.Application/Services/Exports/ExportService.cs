using JobPlatform.Reporting.Application.DTOs.Exports;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.Exports;

/// <summary>Logic shared by the export request handlers.</summary>
internal sealed class ExportService
{
    private readonly IReportAccessGuard _guard;
    private readonly IReportExportRepository _exports;
    private readonly TimeProvider _clock;

    public ExportService(IReportAccessGuard guard, IReportExportRepository exports, TimeProvider clock)
    {
        _guard = guard;
        _exports = exports;
        _clock = clock;
    }

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Error?> DeniedAsync(string name, CancellationToken ct) => (await _guard.EnsureAsync(ReportCategory.Custom, name, ct)).Error;

    public async Task<Result<ExportFileDto>> FileOf(ReportExport export, CancellationToken ct)
    {
        if (export.Status != ExportStatus.Ready)
        {
            return Error.Conflict(ReportingErrorCodes.ExportNotReady, "The export is not ready yet.");
        }

        var file = await _exports.GetFileAsync(export.Id, ct);
        return file is null
            ? Error.NotFound(ReportingErrorCodes.NotFound, "The export file is no longer available.")
            : new ExportFileDto(file.FileName, file.ContentType, file.Content);
    }

    public static ExportDto ToDto(ReportExport e, bool reused) => new(e.Id, e.RefKind.ToString(), e.RefId, e.Format.ToString(), e.Status.ToString(), e.ResultRef, e.FailureReason,
        e.RequestedAtUtc, e.CompletedAtUtc, reused);
}
