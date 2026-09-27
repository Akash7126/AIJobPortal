using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.Reporting.Application;

// US-3.5.4-05 export (also BC-07's Administrator Report, Q-05).

/// <summary>Idempotent (INV-06): an identical request while one is Queued or Generating returns that job (Reused = true) instead of starting another.</summary>
public sealed record RequestReportExportCommand(string RefKind, Guid? RefId, IReadOnlyDictionary<string, string>? Parameters, string Format)
    : CustomCommandRequest, ICommand<ExportDto>, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ReportingErrorCodes.ExportDuplicate;
}

public sealed record GetReportExportQuery(Guid Id) : CustomRequest, IQuery<ExportDto>;

public sealed record ExportFileDto(string FileName, string ContentType, byte[] Content);

public sealed record DownloadReportExportQuery(Guid Id) : CustomRequest, IQuery<ExportFileDto>;

/// <summary>Anonymous download through the signed, expiring link carried by ReportDistributionRequested.</summary>
public sealed record DownloadSharedReportQuery(Guid Id, long Expires, string Signature) : IQuery<ExportFileDto>;

/// <summary>Worker command: generates the file of one queued export and drives it to Ready or Failed.</summary>
public sealed record GenerateReportExportCommand(Guid ExportId) : ICommand<ExportDto>;

/// <summary>RequestReportExportValidator (handover 7): format and reference kind known; the referenced report is checked to exist by the handler.</summary>
public sealed class RequestReportExportValidator : AbstractValidator<RequestReportExportCommand>
{
    public RequestReportExportValidator()
    {
        RuleFor(x => x.Format).Must(f => Enum.TryParse<ReportFormat>(f, true, out _)).WithErrorCode("VAL.Format.Unknown");
        RuleFor(x => x.RefKind).Must(k => Enum.TryParse<ReportRefKind>(k, true, out _)).WithErrorCode("VAL.RefKind.Unknown");
        RuleFor(x => x.RefId).NotNull().When(x => Enum.TryParse<ReportRefKind>(x.RefKind, true, out var k) && k != ReportRefKind.LaborMarket).WithErrorCode("VAL.RefId.Required");
        RuleFor(x => x.Parameters).Must(p => p is null || (p.Count <= 20 && p.All(kv => kv.Key.Length <= 50 && kv.Value.Length <= 200))).WithErrorCode("VAL.Parameters.Invalid");
    }
}

internal sealed class ExportHandlers :
    ICommandHandler<RequestReportExportCommand, ExportDto>,
    IQueryHandler<GetReportExportQuery, ExportDto>,
    IQueryHandler<DownloadReportExportQuery, ExportFileDto>,
    IQueryHandler<DownloadSharedReportQuery, ExportFileDto>,
    ICommandHandler<GenerateReportExportCommand, ExportDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly IReportExportRepository _exports;
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly ExportContentBuilder _content;
    private readonly IReportLinkSigner _signer;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExportHandlers> _logger;

    public ExportHandlers(IReportAccessGuard guard, IReportExportRepository exports, IReportTemplateRepository templates, ISavedReportRepository saved,
        ExportContentBuilder content, IReportLinkSigner signer, ICurrentUser user, TimeProvider clock, ILogger<ExportHandlers> logger)
    {
        _guard = guard;
        _exports = exports;
        _templates = templates;
        _saved = saved;
        _content = content;
        _signer = signer;
        _user = user;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private async Task<Error?> DeniedAsync(string name, CancellationToken ct) => (await _guard.EnsureAsync(ReportCategory.Custom, name, ct)).Error;

    public async Task<Result<ExportDto>> Handle(RequestReportExportCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(RequestReportExportCommand), ct) is { } denied)
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
            return ToDto(running, true);
        }

        var export = ReportExport.Request(administrator, kind, request.RefId, format, parameters, Now);
        _exports.Add(export);
        return ToDto(export, false);
    }

    public async Task<Result<ExportDto>> Handle(GetReportExportQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(GetReportExportQuery), ct) is { } denied)
        {
            return denied;
        }

        var export = await _exports.GetAsync(request.Id, ct);
        return export is null || export.RequestedBy != _user.UserId
            ? Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.")
            : ToDto(export, false);
    }

    public async Task<Result<ExportFileDto>> Handle(DownloadReportExportQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(DownloadReportExportQuery), ct) is { } denied)
        {
            return denied;
        }

        var export = await _exports.GetAsync(request.Id, ct);
        if (export is null || export.RequestedBy != _user.UserId)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.");
        }

        return await FileOf(export, ct);
    }

    public async Task<Result<ExportFileDto>> Handle(DownloadSharedReportQuery request, CancellationToken ct)
    {
        if (!_signer.Verify(request.Id, request.Expires, request.Signature, Now))
        {
            return Error.Forbidden(ReportingErrorCodes.LinkInvalid, "The link is invalid or has expired.");
        }

        var export = await _exports.GetAsync(request.Id, ct);
        return export is null ? Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.") : await FileOf(export, ct);
    }

    private async Task<Result<ExportFileDto>> FileOf(ReportExport export, CancellationToken ct)
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

    public async Task<Result<ExportDto>> Handle(GenerateReportExportCommand request, CancellationToken ct)
    {
        var export = await _exports.GetAsync(request.ExportId, ct);
        if (export is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The export was not found.");
        }

        if (export.Status != ExportStatus.Queued)
        {
            return ToDto(export, false);
        }

        export.StartGenerating();
        try
        {
            var file = await _content.BuildAsync(export.RefKind, export.RefId, export.Parameters, export.Format, ct);
            if (file is null)
            {
                export.Fail("The referenced report no longer exists.", Now);
            }
            else
            {
                _exports.AddFile(ReportExportFile.For(export.Id, file.FileName, file.ContentType, file.Content, Now));
                export.Complete($"/api/v1/admin/reports/exports/{export.Id}/file", Now);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Export {ExportId} failed", export.Id);
            export.Fail(ex.Message, Now);
        }

        return ToDto(export, false);
    }

    private static ExportDto ToDto(ReportExport e, bool reused) => new(e.Id, e.RefKind.ToString(), e.RefId, e.Format.ToString(), e.Status.ToString(), e.ResultRef, e.FailureReason,
        e.RequestedAtUtc, e.CompletedAtUtc, reused);
}

/// <summary>Drives queued exports to Ready or Failed, one command (and transaction) per export. Called by the export worker.</summary>
public sealed class ExportGenerationRunner
{
    private readonly IReportExportRepository _exports;
    private readonly ISender _sender;

    public ExportGenerationRunner(IReportExportRepository exports, ISender sender)
    {
        _exports = exports;
        _sender = sender;
    }

    public async Task<int> RunOnceAsync(int batch, CancellationToken ct)
    {
        var queued = await _exports.ListQueuedAsync(batch, ct);
        foreach (var export in queued)
        {
            await _sender.Send(new GenerateReportExportCommand(export.Id), ct);
        }

        return queued.Count;
    }
}

/// <summary>Runs every due schedule, one command per schedule. Called by the schedule worker under a leader lock.</summary>
public sealed class ScheduleRunner
{
    private readonly IReportScheduleRepository _schedules;
    private readonly ISender _sender;
    private readonly TimeProvider _clock;

    public ScheduleRunner(IReportScheduleRepository schedules, ISender sender, TimeProvider clock)
    {
        _schedules = schedules;
        _sender = sender;
        _clock = clock;
    }

    public async Task<int> RunDueAsync(int batch, CancellationToken ct)
    {
        var ran = 0;
        foreach (var schedule in await _schedules.ListDueAsync(_clock.GetUtcNow().UtcDateTime, batch, ct))
        {
            var result = await _sender.Send(new RunScheduledReportCommand(schedule.Id), ct);
            ran += result.IsSuccess && result.Value ? 1 : 0;
        }

        return ran;
    }
}
