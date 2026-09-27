using JobPlatform.AuditLogging.Domain;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AuditLogging.Application.Ingestion;

/// <summary>
/// Application service behind every inbox handler: dedupes by (source BC, source message id, category) (INV-01), records the immutable entry and updates the
/// projections. Handlers never write tables directly (foundation 9.4). Illegal or out-of-order projection transitions are ignored and logged as anomalies.
/// </summary>
public sealed class AuditIngestion
{
    private readonly IAuditEntryRepository _entries;
    private readonly ISyncJobStatusRepository _sync;
    private readonly IUsageCounterRepository _usage;
    private readonly IJobStatusHistoryRepository _history;
    private readonly INotificationLogRepository _notifications;
    private readonly IEmployerDashboardRepository _dashboards;
    private readonly ICandidateInsightRepository _insights;
    private readonly RetentionPolicy _retention;
    private readonly ILogger<AuditIngestion> _logger;

    public AuditIngestion(IAuditEntryRepository entries, ISyncJobStatusRepository sync, IUsageCounterRepository usage, IJobStatusHistoryRepository history,
        INotificationLogRepository notifications, IEmployerDashboardRepository dashboards, ICandidateInsightRepository insights, RetentionPolicy retention,
        ILogger<AuditIngestion> logger)
    {
        _entries = entries;
        _sync = sync;
        _usage = usage;
        _history = history;
        _notifications = notifications;
        _dashboards = dashboards;
        _insights = insights;
        _retention = retention;
        _logger = logger;
    }

    /// <summary>Records one entry unless the same source message already produced one for this category. Returns false for a redelivery.</summary>
    public async Task<bool> RecordAsync(string sourceBc, Guid sourceMessageId, AuditCategory category, DateTime occurredAtUtc, Guid? actorId, string? actorType,
        string subjectType, string subjectId, OwnerScope scope, string action, AuditOutcome outcome, string? code, IReadOnlyDictionary<string, string>? details,
        CancellationToken ct)
    {
        if (await _entries.ExistsAsync(sourceBc, sourceMessageId, category, ct))
        {
            _logger.LogInformation("Duplicate delivery of {SourceBc} message {MessageId} ({Category}) ignored", sourceBc, sourceMessageId, category);
            return false;
        }

        _entries.Add(AuditEntry.Record(sourceBc, sourceMessageId, category, occurredAtUtc, actorId, actorType, subjectType, subjectId, scope, action, outcome, code,
            details, _retention));
        return true;
    }

    // ---------------------------------------------------------------- sync status (US-3.1.3-10, US-3.4.1-04)

    /// <summary>A job was received and confirmed by the platform. A failed job goes back through Pending first (retry), then Synced.</summary>
    public async Task MarkSyncedAsync(string platformJobId, Guid sourcePlatformId, Guid partnerId, DateTime atUtc, long version, CancellationToken ct)
    {
        var status = await _sync.GetAsync(platformJobId, ct);
        if (status is null)
        {
            status = SyncJobStatus.Received(platformJobId, sourcePlatformId, partnerId, atUtc);
            _sync.Add(status);
        }

        if (status.Status == SyncStatus.Failed)
        {
            status.Retry(atUtc);
        }

        if (!status.MarkSynced(atUtc, version))
        {
            Anomaly(platformJobId, status.Status, "MarkSynced");
        }
    }

    public async Task MarkFailedAsync(string platformJobId, Guid sourcePlatformId, Guid partnerId, string reasonCode, bool isRetry, DateTime atUtc, CancellationToken ct)
    {
        var status = await _sync.GetAsync(platformJobId, ct);
        if (status is null)
        {
            status = SyncJobStatus.Received(platformJobId, sourcePlatformId, partnerId, atUtc);
            _sync.Add(status);
        }
        else if (isRetry && status.Status == SyncStatus.Failed)
        {
            // 3.4.1-04 AC-02: a retry goes Failed to Pending, then fails again.
            status.Retry(atUtc);
        }

        if (!status.MarkFailed(reasonCode, atUtc))
        {
            Anomaly(platformJobId, status.Status, "MarkFailed");
        }
    }

    public async Task ArchiveSyncAsync(string platformJobId, DateTime atUtc, long version, CancellationToken ct)
    {
        var status = await _sync.GetAsync(platformJobId, ct);
        if (status is null || !status.Archive(atUtc, version))
        {
            Anomaly(platformJobId, status?.Status, "Archive");
        }
    }

    public async Task CountSubmissionAsync(Guid partnerId, DateTime atUtc, CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(atUtc);
        var counter = await _usage.GetAsync(partnerId, day, ct);
        if (counter is null)
        {
            counter = IntegrationUsageDaily.For(partnerId, day);
            _usage.Add(counter);
        }

        counter.CountSubmission();
    }

    // ---------------------------------------------------------------- job status history (US-3.2.4-02)

    public async Task RecordJobStatusAsync(Guid sourceMessageId, Guid jobPostingId, Guid employerId, string? from, string to, string? reason, DateTime atUtc,
        CancellationToken ct)
    {
        if (await _history.ExistsAsync(sourceMessageId, ct))
        {
            return;
        }

        _history.Add(JobStatusHistoryEntry.Record(sourceMessageId, jobPostingId, employerId, from, to, reason, atUtc));
    }

    // ---------------------------------------------------------------- notifications (US-3.6.x)

    public async Task RecordNotificationAsync(Guid notificationId, Guid? recipientId, string channel, string category, string maskedRecipient, string? subject,
        string status, DateTime atUtc, CancellationToken ct)
    {
        if (await _notifications.GetAsync(notificationId, ct) is not null)
        {
            return;
        }

        _notifications.Add(NotificationLogEntry.Record(notificationId, recipientId, channel, category, maskedRecipient, subject, status, atUtc));
    }

    public async Task UpdateNotificationStatusAsync(Guid notificationId, string status, DateTime atUtc, CancellationToken ct)
    {
        var entry = await _notifications.GetAsync(notificationId, ct);
        if (entry is null)
        {
            // Out-of-order arrival: the status update is ahead of NotificationSent. Retry later through the inbox.
            throw new InvalidOperationException($"Notification {notificationId} is not known yet.");
        }

        entry.UpdateStatus(status, atUtc);
    }

    // ---------------------------------------------------------------- employer dashboard (US-3.1.2-07)

    public async Task<EmployerDashboard> DashboardAsync(Guid employerId, DateTime atUtc, CancellationToken ct)
    {
        var dashboard = await _dashboards.GetAsync(employerId, ct);
        if (dashboard is null)
        {
            dashboard = EmployerDashboard.Open(employerId, atUtc);
            _dashboards.Add(dashboard);
        }

        return dashboard;
    }

    // ---------------------------------------------------------------- candidate insight (US-3.3.3-06)

    public async Task UpsertInsightAsync(Guid insightId, Guid jobPostingId, Guid employerId, Guid candidateProfileId, string? availability, decimal? salary,
        decimal? fit, IReadOnlyList<string> withheld, DateTime atUtc, CancellationToken ct)
    {
        var existing = await _insights.GetAsync(insightId, ct);
        if (existing is null)
        {
            _insights.Add(CandidateInsightRecord.Compute(insightId, jobPostingId, employerId, candidateProfileId, availability, salary, fit, withheld, atUtc));
        }
        else
        {
            existing.Refresh(availability, salary, fit, withheld, atUtc);
        }
    }

    private void Anomaly(string platformJobId, SyncStatus? current, string transition) =>
        _logger.LogWarning("Ignored out-of-order or illegal sync transition {Transition} for job {PlatformJobId} (current {Status})", transition, platformJobId,
            current?.ToString() ?? "unknown");
}
