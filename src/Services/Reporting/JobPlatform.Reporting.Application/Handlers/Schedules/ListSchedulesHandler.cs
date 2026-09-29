using JobPlatform.Reporting.Application.DTOs.Schedules;
using JobPlatform.Reporting.Application.Queries.Schedules;
using JobPlatform.Reporting.Application.Services.Schedules;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Schedules;

internal sealed class ListSchedulesHandler : IQueryHandler<ListSchedulesQuery, IReadOnlyList<ScheduleDto>>
{
    private readonly IReportScheduleRepository _schedules;
    private readonly ScheduleService _scheduleService;

    public ListSchedulesHandler(IReportScheduleRepository schedules, ScheduleService scheduleService)
    {
        _schedules = schedules;
        _scheduleService = scheduleService;
    }

    public async Task<Result<IReadOnlyList<ScheduleDto>>> Handle(ListSchedulesQuery request, CancellationToken ct) =>
        await _scheduleService.DeniedAsync(nameof(ListSchedulesQuery), ct) is { } denied ? denied : (await _schedules.ListAsync(ct)).Select(ScheduleService.ToDto).ToList();
}
