using JobPlatform.Reporting.Application.DTOs.Schedules;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.Schedules;

/// <summary>Logic shared by the schedule request handlers.</summary>
internal sealed class ScheduleService
{
    private readonly IReportAccessGuard _guard;
    private readonly TimeProvider _clock;

    public ScheduleService(IReportAccessGuard guard, TimeProvider clock)
    {
        _guard = guard;
        _clock = clock;
    }

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Error?> DeniedAsync(string name, CancellationToken ct) => (await _guard.EnsureAsync(ReportCategory.Custom, name, ct)).Error;

    public static ScheduleDto ToDto(ReportSchedule s) => new(s.Id, s.Name, s.TemplateId, s.SavedReportId, s.Interval.ToString(), s.CronText, s.Recipients, s.Format.ToString(),
        s.NextRunAtUtc, s.LastRunAtUtc, s.IsActive);
}
