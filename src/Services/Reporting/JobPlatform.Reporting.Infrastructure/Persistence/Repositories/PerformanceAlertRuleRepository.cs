using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

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
