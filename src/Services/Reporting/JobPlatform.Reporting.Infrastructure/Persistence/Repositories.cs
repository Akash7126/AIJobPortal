using JobPlatform.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

// Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).

internal sealed class RetentionPolicyRepository(ReportingDbContext db) : IRetentionPolicyRepository
{
    public Task<ActivityLogRetentionPolicy?> GetAsync(CancellationToken ct = default) =>
        db.RetentionPolicies.FirstOrDefaultAsync(p => p.Id == ActivityLogRetentionPolicy.SingletonId, ct);

    public void Add(ActivityLogRetentionPolicy policy) => db.RetentionPolicies.Add(policy);
}

internal sealed class ReportTemplateRepository(ReportingDbContext db) : IReportTemplateRepository
{
    public Task<ReportTemplate?> GetAsync(Guid id, CancellationToken ct = default) => db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ReportTemplate>> ListAsync(CancellationToken ct = default) => await db.ReportTemplates.OrderBy(t => t.Name).ToListAsync(ct);

    public void Add(ReportTemplate template) => db.ReportTemplates.Add(template);
}

internal sealed class ReportScheduleRepository(ReportingDbContext db) : IReportScheduleRepository
{
    public Task<ReportSchedule?> GetAsync(Guid id, CancellationToken ct = default) => db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ReportSchedule>> ListAsync(CancellationToken ct = default) => await db.ReportSchedules.OrderBy(s => s.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await db.ReportSchedules.Where(s => s.IsActive && s.NextRunAtUtc <= nowUtc).OrderBy(s => s.NextRunAtUtc).Take(take).ToListAsync(ct);

    public void Add(ReportSchedule schedule) => db.ReportSchedules.Add(schedule);

    public void Remove(ReportSchedule schedule) => db.ReportSchedules.Remove(schedule);
}

internal sealed class SavedReportRepository(ReportingDbContext db) : ISavedReportRepository
{
    public Task<SavedReport?> GetAsync(Guid id, CancellationToken ct = default) => db.SavedReports.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<SavedReport>> ListOwnedAsync(Guid ownerId, CancellationToken ct = default) =>
        await db.SavedReports.Where(r => r.OwnerId == ownerId && !r.IsArchived).OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<SavedReport>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await db.SavedReports.Where(r => !r.IsArchived && r.RetainUntilUtc < nowUtc).OrderBy(r => r.RetainUntilUtc).Take(take).ToListAsync(ct);

    public void Add(SavedReport report) => db.SavedReports.Add(report);

    public void Remove(SavedReport report) => db.SavedReports.Remove(report);
}

internal sealed class ReportExportRepository(ReportingDbContext db) : IReportExportRepository
{
    public Task<ReportExport?> GetAsync(Guid id, CancellationToken ct = default) => db.ReportExports.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<ReportExport?> FindRunningAsync(Guid requestedBy, string parametersHash, CancellationToken ct = default) =>
        db.ReportExports.FirstOrDefaultAsync(e => e.RequestedBy == requestedBy && e.ParametersHash == parametersHash
                                                  && (e.Status == ExportStatus.Queued || e.Status == ExportStatus.Generating), ct);

    public async Task<IReadOnlyList<ReportExport>> ListQueuedAsync(int take, CancellationToken ct = default) =>
        await db.ReportExports.Where(e => e.Status == ExportStatus.Queued).OrderBy(e => e.RequestedAtUtc).Take(take).ToListAsync(ct);

    public Task<ReportExportFile?> GetFileAsync(Guid exportId, CancellationToken ct = default) => db.ReportExportFiles.FirstOrDefaultAsync(f => f.Id == exportId, ct);

    public void Add(ReportExport export) => db.ReportExports.Add(export);

    public void AddFile(ReportExportFile file) => db.ReportExportFiles.Add(file);
}

internal sealed class ReportAccessRuleRepository(ReportingDbContext db) : IReportAccessRuleRepository
{
    public async Task<IReadOnlyList<ReportAccessRule>> ListAsync(CancellationToken ct = default) => await db.ReportAccessRules.AsNoTracking().OrderBy(r => r.Role).ToListAsync(ct);

    public Task<ReportAccessRule?> GetByRoleAsync(string role, CancellationToken ct = default) => db.ReportAccessRules.FirstOrDefaultAsync(r => r.Role == role, ct);

    public void Add(ReportAccessRule rule) => db.ReportAccessRules.Add(rule);

    public void AddDecision(ReportAccessDecision decision) => db.ReportAccessDecisions.Add(decision);
}

internal sealed class PerformanceAlertRuleRepository(ReportingDbContext db) : IPerformanceAlertRuleRepository
{
    public Task<PerformanceAlertRule?> GetAsync(Guid id, CancellationToken ct = default) => db.PerformanceAlertRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<PerformanceAlertRule>> ListAsync(CancellationToken ct = default) => await db.PerformanceAlertRules.OrderBy(r => r.Metric).ToListAsync(ct);

    public async Task<IReadOnlyList<PerformanceAlertRule>> ListEnabledAsync(CancellationToken ct = default) =>
        await db.PerformanceAlertRules.Where(r => r.IsEnabled).ToListAsync(ct);

    public Task<bool> HasAlertSinceAsync(Guid ruleId, DateTime sinceUtc, CancellationToken ct = default) =>
        db.PerformanceAlerts.AnyAsync(a => a.RuleId == ruleId && a.RaisedAtUtc >= sinceUtc, ct);

    public async Task<IReadOnlyList<PerformanceAlert>> ListAlertsAsync(int take, CancellationToken ct = default) =>
        await db.PerformanceAlerts.AsNoTracking().OrderByDescending(a => a.RaisedAtUtc).Take(take).ToListAsync(ct);

    public Task<int> PurgeExpiredAlertsAsync(DateTime nowUtc, CancellationToken ct = default) =>
        db.PerformanceAlerts.Where(a => a.RetainUntilUtc < nowUtc).ExecuteDeleteAsync(ct);

    public void Add(PerformanceAlertRule rule) => db.PerformanceAlertRules.Add(rule);

    public void AddAlert(PerformanceAlert alert) => db.PerformanceAlerts.Add(alert);
}

internal sealed class LaborMarketReportRepository(ReportingDbContext db) : ILaborMarketReportRepository
{
    public Task<LaborMarketReport?> GetByPeriodAsync(string period, CancellationToken ct = default) => db.LaborMarketReports.FirstOrDefaultAsync(r => r.Period == period, ct);

    public async Task<IReadOnlyList<LaborMarketReport>> ListAsync(CancellationToken ct = default) => await db.LaborMarketReports.OrderByDescending(r => r.Period).ToListAsync(ct);

    public void Add(LaborMarketReport report) => db.LaborMarketReports.Add(report);
}
