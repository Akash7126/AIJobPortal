namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface IPerformanceAlertRuleRepository
{
    Task<PerformanceAlertRule?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PerformanceAlertRule>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PerformanceAlertRule>> ListEnabledAsync(CancellationToken ct = default);

    /// <summary>Alerts of the rule raised at or after <paramref name="sinceUtc"/> (used to avoid re-alerting inside the window).</summary>
    Task<bool> HasAlertSinceAsync(Guid ruleId, DateTime sinceUtc, CancellationToken ct = default);

    Task<IReadOnlyList<PerformanceAlert>> ListAlertsAsync(int take, CancellationToken ct = default);

    Task<int> PurgeExpiredAlertsAsync(DateTime nowUtc, CancellationToken ct = default);

    void Add(PerformanceAlertRule rule);

    void AddAlert(PerformanceAlert alert);
}
