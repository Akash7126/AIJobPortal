using JobPlatform.Reporting.Application.Commands.Schedules;
using JobPlatform.Reporting.Application.DTOs.Schedules;
using JobPlatform.Reporting.Application.Services.Schedules;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Schedules;

internal sealed class ConfigureReportScheduleHandler : ICommandHandler<ConfigureReportScheduleCommand, ScheduleDto>
{
    private readonly IReportScheduleRepository _schedules;
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly ICurrentUser _user;
    private readonly ScheduleService _scheduleService;

    public ConfigureReportScheduleHandler(IReportScheduleRepository schedules, IReportTemplateRepository templates, ISavedReportRepository saved, ICurrentUser user, ScheduleService scheduleService)
    {
        _schedules = schedules;
        _templates = templates;
        _saved = saved;
        _user = user;
        _scheduleService = scheduleService;
    }

    public async Task<Result<ScheduleDto>> Handle(ConfigureReportScheduleCommand request, CancellationToken ct)
    {
        if (await _scheduleService.DeniedAsync(nameof(ConfigureReportScheduleCommand), ct) is { } denied)
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

            existing.Reconfigure(request.Name, request.TemplateId, request.SavedReportId, interval, request.Cron, request.Recipients, format, _scheduleService.Now);
            schedule = existing;
        }
        else
        {
            schedule = ReportSchedule.Create(request.Name, request.TemplateId, request.SavedReportId, interval, request.Cron, request.Recipients, format, _user.UserId!.Value, _scheduleService.Now);
            _schedules.Add(schedule);
        }

        return ScheduleService.ToDto(schedule);
    }
}
