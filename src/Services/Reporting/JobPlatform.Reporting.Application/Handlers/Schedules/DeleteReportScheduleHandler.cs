using JobPlatform.Reporting.Application.Commands.Schedules;
using JobPlatform.Reporting.Application.Services.Schedules;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.Reporting.Application.Handlers.Schedules;

internal sealed class DeleteReportScheduleHandler : ICommandHandler<DeleteReportScheduleCommand, Unit>
{
    private readonly IReportScheduleRepository _schedules;
    private readonly ScheduleService _scheduleService;

    public DeleteReportScheduleHandler(IReportScheduleRepository schedules, ScheduleService scheduleService)
    {
        _schedules = schedules;
        _scheduleService = scheduleService;
    }

    public async Task<Result<Unit>> Handle(DeleteReportScheduleCommand request, CancellationToken ct)
    {
        if (await _scheduleService.DeniedAsync(nameof(DeleteReportScheduleCommand), ct) is { } denied)
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
}
