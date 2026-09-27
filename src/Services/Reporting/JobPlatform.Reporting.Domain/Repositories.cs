namespace JobPlatform.Reporting.Domain;

/// <summary>Aggregate-oriented repositories. The read side never uses them (see IAnalyticsQueryService in the application layer).</summary>
public interface IRetentionPolicyRepository
{
    Task<ActivityLogRetentionPolicy?> GetAsync(CancellationToken ct = default);

    void Add(ActivityLogRetentionPolicy policy);
}

public interface IReportTemplateRepository
{
    Task<ReportTemplate?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReportTemplate>> ListAsync(CancellationToken ct = default);

    void Add(ReportTemplate template);
}

public interface IReportScheduleRepository
{
    Task<ReportSchedule?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReportSchedule>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    void Add(ReportSchedule schedule);

    void Remove(ReportSchedule schedule);
}

public interface ISavedReportRepository
{
    Task<SavedReport?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SavedReport>> ListOwnedAsync(Guid ownerId, CancellationToken ct = default);

    Task<IReadOnlyList<SavedReport>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    void Add(SavedReport report);

    void Remove(SavedReport report);
}

public interface IReportExportRepository
{
    Task<ReportExport?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The Queued or Generating export with the same administrator and parameter hash (INV-06), if any.</summary>
    Task<ReportExport?> FindRunningAsync(Guid requestedBy, string parametersHash, CancellationToken ct = default);

    Task<IReadOnlyList<ReportExport>> ListQueuedAsync(int take, CancellationToken ct = default);

    Task<ReportExportFile?> GetFileAsync(Guid exportId, CancellationToken ct = default);

    void Add(ReportExport export);

    void AddFile(ReportExportFile file);
}

public interface IReportAccessRuleRepository
{
    Task<IReadOnlyList<ReportAccessRule>> ListAsync(CancellationToken ct = default);

    Task<ReportAccessRule?> GetByRoleAsync(string role, CancellationToken ct = default);

    void Add(ReportAccessRule rule);

    void AddDecision(ReportAccessDecision decision);
}

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

public interface ILaborMarketReportRepository
{
    Task<LaborMarketReport?> GetByPeriodAsync(string period, CancellationToken ct = default);

    Task<IReadOnlyList<LaborMarketReport>> ListAsync(CancellationToken ct = default);

    void Add(LaborMarketReport report);
}
