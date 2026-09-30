using JobPlatform.Reporting.Application.Commands.Exports;
using JobPlatform.Reporting.Application.Commands.Schedules;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application;

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
