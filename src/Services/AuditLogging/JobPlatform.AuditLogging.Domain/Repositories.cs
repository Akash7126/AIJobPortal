namespace JobPlatform.AuditLogging.Domain;

/// <summary>Aggregate-oriented repositories. The read side never uses them (see IAuditReadStore in the application layer).</summary>
public interface IAuditEntryRepository
{
    /// <summary>INV-01: one entry per (source BC, source message id, category), so a redelivery never duplicates a log line.</summary>
    Task<bool> ExistsAsync(string sourceBc, Guid sourceMessageId, AuditCategory category, CancellationToken ct = default);

    void Add(AuditEntry entry);

    Task<IReadOnlyList<AuditEntry>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default);
}

public interface ISyncJobStatusRepository
{
    Task<SyncJobStatus?> GetAsync(string platformJobId, CancellationToken ct = default);

    void Add(SyncJobStatus status);
}

public interface IUsageCounterRepository
{
    Task<IntegrationUsageDaily?> GetAsync(Guid partnerId, DateOnly day, CancellationToken ct = default);

    void Add(IntegrationUsageDaily counter);
}

public interface IJobStatusHistoryRepository
{
    Task<bool> ExistsAsync(Guid sourceMessageId, CancellationToken ct = default);

    void Add(JobStatusHistoryEntry entry);
}

public interface INotificationLogRepository
{
    Task<NotificationLogEntry?> GetAsync(Guid notificationId, CancellationToken ct = default);

    void Add(NotificationLogEntry entry);
}

public interface IEmployerDashboardRepository
{
    Task<EmployerDashboard?> GetAsync(Guid employerId, CancellationToken ct = default);

    void Add(EmployerDashboard dashboard);
}

public interface ICandidateInsightRepository
{
    Task<CandidateInsightRecord?> GetAsync(Guid insightId, CancellationToken ct = default);

    void Add(CandidateInsightRecord insight);
}

public interface IExportJobRepository
{
    Task<ExportJob?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The Queued or Generating job with the same administrator, type and parameter hash (INV-04), if any.</summary>
    Task<ExportJob?> FindInProgressAsync(Guid requestedBy, ReportType type, string parametersHash, CancellationToken ct = default);

    Task<IReadOnlyList<ExportJob>> ListQueuedAsync(int take, CancellationToken ct = default);

    void Add(ExportJob job);
}
