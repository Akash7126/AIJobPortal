using JobPlatform.AuditLogging.Application.Ingestion;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JobPlatform.AuditLogging.Application.UnitTests;

/// <summary>In-memory implementation of every repository so ingestion handlers can be tested against real state (no mocks of behaviour).</summary>
public sealed class FakeStore :
    IAuditEntryRepository, ISyncJobStatusRepository, IUsageCounterRepository, IJobStatusHistoryRepository, INotificationLogRepository,
    IEmployerDashboardRepository, ICandidateInsightRepository, IExportJobRepository
{
    public List<AuditEntry> Entries { get; } = new();
    public List<SyncJobStatus> Syncs { get; } = new();
    public List<IntegrationUsageDaily> Usage { get; } = new();
    public List<JobStatusHistoryEntry> History { get; } = new();
    public List<NotificationLogEntry> Notifications { get; } = new();
    public List<EmployerDashboard> Dashboards { get; } = new();
    public List<CandidateInsightRecord> Insights { get; } = new();
    public List<ExportJob> Jobs { get; } = new();

    public Task<bool> ExistsAsync(string sourceBc, Guid sourceMessageId, AuditCategory category, CancellationToken ct = default) =>
        Task.FromResult(Entries.Any(e => e.SourceBc == sourceBc && e.SourceMessageId == sourceMessageId && e.Category == category));

    void IAuditEntryRepository.Add(AuditEntry entry) => Entries.Add(entry);

    public Task<IReadOnlyList<AuditEntry>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditEntry>>(Entries.Where(e => !e.IsArchived && e.RetainUntilUtc < nowUtc).Take(take).ToList());

    Task<SyncJobStatus?> ISyncJobStatusRepository.GetAsync(string platformJobId, CancellationToken ct) => Task.FromResult(Syncs.FirstOrDefault(s => s.Id == platformJobId));

    void ISyncJobStatusRepository.Add(SyncJobStatus status) => Syncs.Add(status);

    Task<IntegrationUsageDaily?> IUsageCounterRepository.GetAsync(Guid partnerId, DateOnly day, CancellationToken ct) =>
        Task.FromResult(Usage.FirstOrDefault(u => u.PartnerId == partnerId && u.Day == day));

    void IUsageCounterRepository.Add(IntegrationUsageDaily counter) => Usage.Add(counter);

    Task<bool> IJobStatusHistoryRepository.ExistsAsync(Guid sourceMessageId, CancellationToken ct) => Task.FromResult(History.Any(h => h.SourceMessageId == sourceMessageId));

    void IJobStatusHistoryRepository.Add(JobStatusHistoryEntry entry) => History.Add(entry);

    Task<NotificationLogEntry?> INotificationLogRepository.GetAsync(Guid notificationId, CancellationToken ct) =>
        Task.FromResult(Notifications.FirstOrDefault(n => n.Id == notificationId));

    void INotificationLogRepository.Add(NotificationLogEntry entry) => Notifications.Add(entry);

    Task<EmployerDashboard?> IEmployerDashboardRepository.GetAsync(Guid employerId, CancellationToken ct) => Task.FromResult(Dashboards.FirstOrDefault(d => d.Id == employerId));

    void IEmployerDashboardRepository.Add(EmployerDashboard dashboard) => Dashboards.Add(dashboard);

    Task<CandidateInsightRecord?> ICandidateInsightRepository.GetAsync(Guid insightId, CancellationToken ct) => Task.FromResult(Insights.FirstOrDefault(i => i.Id == insightId));

    void ICandidateInsightRepository.Add(CandidateInsightRecord insight) => Insights.Add(insight);

    Task<ExportJob?> IExportJobRepository.GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Jobs.FirstOrDefault(j => j.Id == id));

    public Task<ExportJob?> FindInProgressAsync(Guid requestedBy, ReportType type, string parametersHash, CancellationToken ct = default) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.RequestedBy == requestedBy && j.ReportType == type && j.ParametersHash == parametersHash && j.IsInProgress));

    public Task<IReadOnlyList<ExportJob>> ListQueuedAsync(int take, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExportJob>>(Jobs.Where(j => j.Status == ExportStatus.Queued).Take(take).ToList());

    void IExportJobRepository.Add(ExportJob job) => Jobs.Add(job);

    public AuditIngestion Ingestion(RetentionPolicy? retention = null) =>
        new(this, this, this, this, this, this, this, retention ?? new RetentionPolicy(), NullLogger<AuditIngestion>.Instance);
}

public static class Users
{
    public static ICurrentUser Of(ActorType? actor, Guid? id)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id);
        user.MfaVerified.Returns(actor == ActorType.Administrator);
        return user;
    }
}

public static class Ids
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static (Guid MessageId, DateTime At, Guid CorrelationId) Header() => (Guid.NewGuid(), T0, Guid.NewGuid());
}
