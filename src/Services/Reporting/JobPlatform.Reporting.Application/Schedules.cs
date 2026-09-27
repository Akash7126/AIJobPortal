using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.Reporting.Application;

// US-3.5.4-04 report schedules: configuration plus the worker that runs due schedules and asks BC-13 to e-mail the report (ReportDistributionRequested).

public sealed record ListSchedulesQuery : CustomRequest, IQuery<IReadOnlyList<ScheduleDto>>;

/// <summary>Creates a schedule (Id null) or reconfigures it. Interval defaults to Daily (A-02-013).</summary>
public sealed record ConfigureReportScheduleCommand(Guid? Id, string Name, Guid? TemplateId, Guid? SavedReportId, string? Interval, string? Cron, IReadOnlyList<string> Recipients,
    string Format) : CustomCommandRequest, ICommand<ScheduleDto>;

public sealed record DeleteReportScheduleCommand(Guid Id) : CustomCommandRequest, ICommand;

/// <summary>Worker command: generates the report of one due schedule and requests its distribution. Not exposed over HTTP.</summary>
public sealed record RunScheduledReportCommand(Guid ScheduleId) : ICommand<bool>;

/// <summary>ConfigureReportScheduleValidator (handover 7): valid interval, recipients are valid e-mails (at most 50), format known.</summary>
public sealed class ConfigureReportScheduleValidator : AbstractValidator<ConfigureReportScheduleCommand>
{
    public ConfigureReportScheduleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithErrorCode("VAL.Name.Required").MaximumLength(150).WithErrorCode("VAL.Name.TooLong");
        RuleFor(x => x.Interval).Must(i => i is null || Enum.TryParse<ScheduleInterval>(i, true, out _)).WithErrorCode("VAL.Interval.Unknown");
        RuleFor(x => x.Format).Must(f => Enum.TryParse<ReportFormat>(f, true, out _)).WithErrorCode("VAL.Format.Unknown");
        RuleFor(x => x.Recipients).NotNull().WithErrorCode("VAL.Recipients.Required");
        RuleFor(x => x.Recipients).Must(r => r is null || r.Count is >= 1 and <= ReportSchedule.MaxRecipients).WithErrorCode("VAL.Recipients.Count");
        RuleForEach(x => x.Recipients).Must(r => Email.TryCreate(r, out _)).WithErrorCode("VAL.Recipient.InvalidEmail");
    }
}

internal sealed class ScheduleHandlers :
    IQueryHandler<ListSchedulesQuery, IReadOnlyList<ScheduleDto>>,
    ICommandHandler<ConfigureReportScheduleCommand, ScheduleDto>,
    ICommandHandler<DeleteReportScheduleCommand, Unit>,
    ICommandHandler<RunScheduledReportCommand, bool>
{
    private readonly IReportAccessGuard _guard;
    private readonly IReportScheduleRepository _schedules;
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly IReportExportRepository _exports;
    private readonly ExportContentBuilder _content;
    private readonly IReportLinkSigner _signer;
    private readonly IOptions<ReportingOptions> _options;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly ILogger<ScheduleHandlers> _logger;

    public ScheduleHandlers(IReportAccessGuard guard, IReportScheduleRepository schedules, IReportTemplateRepository templates, ISavedReportRepository saved,
        IReportExportRepository exports, ExportContentBuilder content, IReportLinkSigner signer, IOptions<ReportingOptions> options, ICurrentUser user, TimeProvider clock,
        ILogger<ScheduleHandlers> logger)
    {
        _guard = guard;
        _schedules = schedules;
        _templates = templates;
        _saved = saved;
        _exports = exports;
        _content = content;
        _signer = signer;
        _options = options;
        _user = user;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private async Task<Error?> DeniedAsync(string name, CancellationToken ct) => (await _guard.EnsureAsync(ReportCategory.Custom, name, ct)).Error;

    public async Task<Result<IReadOnlyList<ScheduleDto>>> Handle(ListSchedulesQuery request, CancellationToken ct) =>
        await DeniedAsync(nameof(ListSchedulesQuery), ct) is { } denied ? denied : (await _schedules.ListAsync(ct)).Select(ToDto).ToList();

    public async Task<Result<ScheduleDto>> Handle(ConfigureReportScheduleCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(ConfigureReportScheduleCommand), ct) is { } denied)
        {
            return denied;
        }

        if (request.TemplateId is { } t && await _templates.GetAsync(t, ct) is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The template was not found.");
        }

        if (request.SavedReportId is { } s && await _saved.GetAsync(s, ct) is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The saved report was not found.");
        }

        var interval = request.Interval is null ? (ScheduleInterval?)null : Enum.Parse<ScheduleInterval>(request.Interval, true);
        var format = Enum.Parse<ReportFormat>(request.Format, true);
        ReportSchedule schedule;
        if (request.Id is { } id)
        {
            var existing = await _schedules.GetAsync(id, ct);
            if (existing is null)
            {
                return Error.NotFound(ReportingErrorCodes.NotFound, "The schedule was not found.");
            }

            existing.Reconfigure(request.Name, request.TemplateId, request.SavedReportId, interval, request.Cron, request.Recipients, format, Now);
            schedule = existing;
        }
        else
        {
            schedule = ReportSchedule.Create(request.Name, request.TemplateId, request.SavedReportId, interval, request.Cron, request.Recipients, format, _user.UserId!.Value, Now);
            _schedules.Add(schedule);
        }

        return ToDto(schedule);
    }

    public async Task<Result<Unit>> Handle(DeleteReportScheduleCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(DeleteReportScheduleCommand), ct) is { } denied)
        {
            return denied;
        }

        var schedule = await _schedules.GetAsync(request.Id, ct);
        if (schedule is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The schedule was not found.");
        }

        _schedules.Remove(schedule);
        return Result.Success();
    }

    public async Task<Result<bool>> Handle(RunScheduledReportCommand request, CancellationToken ct)
    {
        var schedule = await _schedules.GetAsync(request.ScheduleId, ct);
        if (schedule is null || !schedule.IsDue(Now))
        {
            return false;
        }

        var kind = schedule.TemplateId is not null ? ReportRefKind.Template : ReportRefKind.SavedReport;
        var refId = schedule.TemplateId ?? schedule.SavedReportId;
        try
        {
            var file = await _content.BuildAsync(kind, refId, new Dictionary<string, string>(), schedule.Format, ct);
            if (file is null)
            {
                _logger.LogWarning("Schedule {ScheduleId} points at a report that no longer exists; the run is skipped", schedule.Id);
                schedule.SkipRun(Now);
                return false;
            }

            var export = ReportExport.Request(schedule.CreatedBy, kind, refId, schedule.Format, new Dictionary<string, string> { ["schedule"] = schedule.Id.ToString() }, Now);
            export.StartGenerating();
            _exports.Add(export);
            _exports.AddFile(ReportExportFile.For(export.Id, file.FileName, file.ContentType, file.Content, Now));
            var expires = Now.Add(_options.Value.LinkLifetime);
            var link = $"{_options.Value.PublicBaseUrl.TrimEnd('/')}/api/v1/reports/shared/{export.Id}?expires={new DateTimeOffset(expires, TimeSpan.Zero).ToUnixTimeSeconds()}&sig={_signer.Sign(export.Id, expires)}";
            export.Complete(link, Now);
            schedule.CompleteRun(link, Now);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Scheduled run of {ScheduleId} failed; retrying at the next occurrence", schedule.Id);
            schedule.SkipRun(Now);
            return false;
        }
    }

    private static ScheduleDto ToDto(ReportSchedule s) => new(s.Id, s.Name, s.TemplateId, s.SavedReportId, s.Interval.ToString(), s.CronText, s.Recipients, s.Format.ToString(),
        s.NextRunAtUtc, s.LastRunAtUtc, s.IsActive);
}
