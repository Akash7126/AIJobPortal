namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface IReportScheduleRepository
{
    Task<ReportSchedule?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReportSchedule>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    void Add(ReportSchedule schedule);

    void Remove(ReportSchedule schedule);
}
