using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class ReportScheduleRepository(ReportingDbContext db) : IReportScheduleRepository
{
    public Task<ReportSchedule?> GetAsync(Guid id, CancellationToken ct = default) => db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ReportSchedule>> ListAsync(CancellationToken ct = default) => await db.ReportSchedules.OrderBy(s => s.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await db.ReportSchedules.Where(s => s.IsActive && s.NextRunAtUtc <= nowUtc).OrderBy(s => s.NextRunAtUtc).Take(take).ToListAsync(ct);

    public void Add(ReportSchedule schedule) => db.ReportSchedules.Add(schedule);

    public void Remove(ReportSchedule schedule) => db.ReportSchedules.Remove(schedule);
}
