using JobPlatform.Reporting.Application.Commands.Schedules;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Services.Schedules;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.Reporting.Application.Handlers.Schedules;

internal sealed class RunScheduledReportHandler : ICommandHandler<RunScheduledReportCommand, bool>
{
    private readonly IReportScheduleRepository _schedules;
    private readonly IReportExportRepository _exports;
    private readonly ExportContentBuilder _content;
    private readonly IReportLinkSigner _signer;
    private readonly IOptions<ReportingOptions> _options;
    private readonly ILogger<RunScheduledReportHandler> _logger;
    private readonly ScheduleService _scheduleService;

    public RunScheduledReportHandler(IReportScheduleRepository schedules, IReportExportRepository exports, ExportContentBuilder content, IReportLinkSigner signer, IOptions<ReportingOptions> options, ILogger<RunScheduledReportHandler> logger, ScheduleService scheduleService)
    {
        _schedules = schedules;
        _exports = exports;
        _content = content;
        _signer = signer;
        _options = options;
        _logger = logger;
        _scheduleService = scheduleService;
    }

    public async Task<Result<bool>> Handle(RunScheduledReportCommand request, CancellationToken ct)
    {
        var schedule = await _schedules.GetAsync(request.ScheduleId, ct);
        if (schedule is null || !schedule.IsDue(_scheduleService.Now))
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
                schedule.SkipRun(_scheduleService.Now);
                return false;
            }

            var export = ReportExport.Request(schedule.CreatedBy, kind, refId, schedule.Format, new Dictionary<string, string> { ["schedule"] = schedule.Id.ToString() }, _scheduleService.Now);
            export.StartGenerating();
            _exports.Add(export);
            _exports.AddFile(ReportExportFile.For(export.Id, file.FileName, file.ContentType, file.Content, _scheduleService.Now));
            var expires = _scheduleService.Now.Add(_options.Value.LinkLifetime);
            var link = $"{_options.Value.PublicBaseUrl.TrimEnd('/')}/api/v1/reports/shared/{export.Id}?expires={new DateTimeOffset(expires, TimeSpan.Zero).ToUnixTimeSeconds()}&sig={_signer.Sign(export.Id, expires)}";
            export.Complete(link, _scheduleService.Now);
            schedule.CompleteRun(link, _scheduleService.Now);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Scheduled run of {ScheduleId} failed; retrying at the next occurrence", schedule.Id);
            schedule.SkipRun(_scheduleService.Now);
            return false;
        }
    }
}
