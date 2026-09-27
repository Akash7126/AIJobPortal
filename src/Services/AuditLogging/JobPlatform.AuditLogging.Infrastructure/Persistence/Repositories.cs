using JobPlatform.AuditLogging.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class AuditEntryRepository : IAuditEntryRepository
{
    private readonly AuditDbContext _db;

    public AuditEntryRepository(AuditDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string sourceBc, Guid sourceMessageId, AuditCategory category, CancellationToken ct = default) =>
        _db.AuditEntries.AnyAsync(e => e.SourceBc == sourceBc && e.SourceMessageId == sourceMessageId && e.Category == category, ct);

    public void Add(AuditEntry entry) => _db.AuditEntries.Add(entry);

    public async Task<IReadOnlyList<AuditEntry>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await _db.AuditEntries.Where(e => !e.IsArchived && e.RetainUntilUtc < nowUtc).OrderBy(e => e.RetainUntilUtc).Take(take).ToListAsync(ct);
}

internal sealed class SyncJobStatusRepository : ISyncJobStatusRepository
{
    private readonly AuditDbContext _db;

    public SyncJobStatusRepository(AuditDbContext db) => _db = db;

    public Task<SyncJobStatus?> GetAsync(string platformJobId, CancellationToken ct = default) =>
        _db.SyncJobStatuses.FirstOrDefaultAsync(s => s.Id == platformJobId, ct);

    public void Add(SyncJobStatus status) => _db.SyncJobStatuses.Add(status);
}

internal sealed class UsageCounterRepository : IUsageCounterRepository
{
    private readonly AuditDbContext _db;

    public UsageCounterRepository(AuditDbContext db) => _db = db;

    public Task<IntegrationUsageDaily?> GetAsync(Guid partnerId, DateOnly day, CancellationToken ct = default) =>
        _db.IntegrationUsageDaily.FirstOrDefaultAsync(u => u.PartnerId == partnerId && u.Day == day, ct);

    public void Add(IntegrationUsageDaily counter) => _db.IntegrationUsageDaily.Add(counter);
}

internal sealed class JobStatusHistoryRepository : IJobStatusHistoryRepository
{
    private readonly AuditDbContext _db;

    public JobStatusHistoryRepository(AuditDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid sourceMessageId, CancellationToken ct = default) =>
        _db.JobStatusHistory.AnyAsync(h => h.SourceMessageId == sourceMessageId, ct);

    public void Add(JobStatusHistoryEntry entry) => _db.JobStatusHistory.Add(entry);
}

internal sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly AuditDbContext _db;

    public NotificationLogRepository(AuditDbContext db) => _db = db;

    public Task<NotificationLogEntry?> GetAsync(Guid notificationId, CancellationToken ct = default) =>
        _db.NotificationLog.FirstOrDefaultAsync(n => n.Id == notificationId, ct);

    public void Add(NotificationLogEntry entry) => _db.NotificationLog.Add(entry);
}

internal sealed class EmployerDashboardRepository : IEmployerDashboardRepository
{
    private readonly AuditDbContext _db;

    public EmployerDashboardRepository(AuditDbContext db) => _db = db;

    public Task<EmployerDashboard?> GetAsync(Guid employerId, CancellationToken ct = default) =>
        _db.EmployerDashboards.FirstOrDefaultAsync(d => d.Id == employerId, ct);

    public void Add(EmployerDashboard dashboard) => _db.EmployerDashboards.Add(dashboard);
}

internal sealed class CandidateInsightRepository : ICandidateInsightRepository
{
    private readonly AuditDbContext _db;

    public CandidateInsightRepository(AuditDbContext db) => _db = db;

    public Task<CandidateInsightRecord?> GetAsync(Guid insightId, CancellationToken ct = default) =>
        _db.CandidateInsights.FirstOrDefaultAsync(i => i.Id == insightId, ct);

    public void Add(CandidateInsightRecord insight) => _db.CandidateInsights.Add(insight);
}

internal sealed class ExportJobRepository : IExportJobRepository
{
    private readonly AuditDbContext _db;

    public ExportJobRepository(AuditDbContext db) => _db = db;

    public Task<ExportJob?> GetAsync(Guid id, CancellationToken ct = default) => _db.ExportJobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<ExportJob?> FindInProgressAsync(Guid requestedBy, ReportType type, string parametersHash, CancellationToken ct = default) =>
        _db.ExportJobs.FirstOrDefaultAsync(j => j.RequestedBy == requestedBy && j.ReportType == type && j.ParametersHash == parametersHash
                                                && (j.Status == ExportStatus.Queued || j.Status == ExportStatus.Generating), ct);

    public async Task<IReadOnlyList<ExportJob>> ListQueuedAsync(int take, CancellationToken ct = default) =>
        await _db.ExportJobs.Where(j => j.Status == ExportStatus.Queued).OrderBy(j => j.RequestedAtUtc).Take(take).ToListAsync(ct);

    public void Add(ExportJob job) => _db.ExportJobs.Add(job);
}
