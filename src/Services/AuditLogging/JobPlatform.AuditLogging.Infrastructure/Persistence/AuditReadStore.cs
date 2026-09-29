using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence;

/// <summary>Read side: AsNoTracking projections straight into DTOs, never through aggregates (foundation section 3.5).</summary>
internal sealed class AuditReadStore : IAuditReadStore
{
    private readonly AuditDbContext _db;

    public AuditReadStore(AuditDbContext db) => _db = db;

    public async Task<PagedResult<AuditEntryDto>> ListEntriesAsync(EntryFilter filter, PageRequest page, CancellationToken ct = default)
    {
        var query = _db.AuditEntries.AsNoTracking().Where(e => filter.Categories.Contains(e.Category));
        if (!filter.IncludeArchived)
        {
            query = query.Where(e => !e.IsArchived);
        }

        if (filter.OwnerId is { } owner)
        {
            query = query.Where(e => e.OwnerId == owner);
        }

        if (filter.SubjectId is { } subject)
        {
            query = query.Where(e => e.SubjectId == subject);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(e => e.OccurredAtUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(e => e.OccurredAtUtc <= to);
        }

        if (filter.Outcome is { } outcome)
        {
            query = query.Where(e => e.Outcome == outcome);
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.OccurredAtUtc).ThenBy(e => e.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<AuditEntryDto>(rows.Select(e => new AuditEntryDto(e.Id, e.Category.ToString(), e.OccurredAtUtc, e.ActorId, e.ActorType, e.SubjectType,
            e.SubjectId, e.Action, e.Outcome.ToString(), e.Code, e.Details, e.IsArchived)).ToList(), page.Page, page.PageSize, total);
    }

    public async Task<SyncDashboardDto> GetSyncDashboardAsync(Guid partnerId, PageRequest page, CancellationToken ct = default)
    {
        var mine = _db.SyncJobStatuses.AsNoTracking().Where(s => s.OwnerId == partnerId);
        var counts = (await mine.GroupBy(s => s.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct)).ToDictionary(c => c.Key, c => c.Count);
        var total = counts.Values.Sum();
        var rows = await mine.OrderByDescending(s => s.UpdatedAtUtc).ThenBy(s => s.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        var jobs = new PagedResult<SyncJobStatusDto>(rows.Select(s => new SyncJobStatusDto(s.Id, s.Status.ToString(), s.ReasonCode, s.UpdatedAtUtc)).ToList(),
            page.Page, page.PageSize, total);
        return new SyncDashboardDto(counts.GetValueOrDefault(SyncStatus.Pending), counts.GetValueOrDefault(SyncStatus.Synced), counts.GetValueOrDefault(SyncStatus.Failed),
            counts.GetValueOrDefault(SyncStatus.Archived), jobs);
    }

    public async Task<IntegrationStatusDto> GetIntegrationStatusAsync(Guid partnerId, DateOnly today, CancellationToken ct = default)
    {
        var mine = _db.SyncJobStatuses.AsNoTracking().Where(s => s.OwnerId == partnerId);
        var pending = await mine.CountAsync(s => s.Status == SyncStatus.Pending, ct);
        var synced = await mine.CountAsync(s => s.Status == SyncStatus.Synced, ct);
        var failed = await mine.CountAsync(s => s.Status == SyncStatus.Failed, ct);
        var archived = await mine.CountAsync(s => s.Status == SyncStatus.Archived, ct);
        var last = await mine.OrderByDescending(s => s.UpdatedAtUtc).Select(s => (DateTime?)s.UpdatedAtUtc).FirstOrDefaultAsync(ct);
        var since = today.AddDays(-30);
        var submitted = await _db.IntegrationUsageDaily.AsNoTracking().Where(u => u.PartnerId == partnerId && u.Day >= since && u.Day <= today)
            .SumAsync(u => (int?)u.Submitted, ct) ?? 0;

        // Health: any failed job degrades the integration; nothing synced yet is "Idle".
        var health = failed > 0 ? "Degraded" : synced + pending + archived == 0 ? "Idle" : "Healthy";
        return new IntegrationStatusDto(health, pending, synced, failed, archived, last, submitted);
    }

    public async Task<UsageStatisticsDto> GetUsageAsync(Guid partnerId, UsageWindow window, CancellationToken ct = default)
    {
        var rows = await _db.IntegrationUsageDaily.AsNoTracking().Where(u => u.PartnerId == partnerId && u.Day >= window.From && u.Day <= window.To)
            .OrderBy(u => u.Day).ToListAsync(ct);
        return new UsageStatisticsDto(window.From, window.To, rows.Sum(r => r.Submitted), rows.Sum(r => r.Matched), rows.Sum(r => r.Viewed),
            rows.Select(r => new UsageDayDto(r.Day, r.Submitted, r.Matched, r.Viewed)).ToList());
    }

    public async Task<JobHistoryOwnerView> GetJobStatusHistoryAsync(Guid jobPostingId, CancellationToken ct = default)
    {
        var rows = await _db.JobStatusHistory.AsNoTracking().Where(h => h.JobPostingId == jobPostingId).OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id).ToListAsync(ct);
        return new JobHistoryOwnerView(rows.FirstOrDefault()?.EmployerId,
            rows.Select(h => new JobStatusHistoryDto(h.JobPostingId, h.EmployerId, h.FromStatus, h.ToStatus, h.Reason, h.ChangedAtUtc)).ToList());
    }

    public async Task<PagedResult<NotificationLogDto>> ListNotificationLogAsync(string[] channels, Guid? recipientId, PageRequest page, CancellationToken ct = default)
    {
        var query = _db.NotificationLog.AsNoTracking().Where(n => channels.Contains(n.Channel));
        if (recipientId is { } recipient)
        {
            query = query.Where(n => n.RecipientId == recipient);
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(n => n.SentAtUtc).ThenBy(n => n.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<NotificationLogDto>(rows.Select(n => new NotificationLogDto(n.Id, n.Channel, n.Category, n.MaskedRecipient, n.Subject, n.Status, n.SentAtUtc))
            .ToList(), page.Page, page.PageSize, total);
    }

    public async Task<EmployerDashboardDto?> GetEmployerDashboardAsync(Guid employerId, CancellationToken ct = default)
    {
        var dashboard = await _db.EmployerDashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == employerId, ct);
        if (dashboard is null)
        {
            return null;
        }

        var items = dashboard.Postings.OrderByDescending(p => p.UpdatedAtUtc).Select(p => new DashboardPostingDto(p.JobPostingId, p.Title, p.Status, p.UpdatedAtUtc)).ToList();
        return new EmployerDashboardDto(dashboard.RegistrationApproved, items.Count, items.Count(i => i.Status == "Active"), dashboard.ShortlistCount, items,
            dashboard.UpdatedAtUtc);
    }

    public async Task<CandidateInsightRaw?> GetCandidateInsightAsync(Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default)
    {
        var insight = await _db.CandidateInsights.AsNoTracking().Where(i => i.CandidateProfileId == candidateProfileId && i.JobPostingId == jobPostingId)
            .OrderByDescending(i => i.ComputedAtUtc).FirstOrDefaultAsync(ct);
        return insight is null
            ? null
            : new CandidateInsightRaw(insight.EmployerId, insight.Availability, insight.ExpectedSalary, insight.FitScore, insight.WithheldFields, insight.ComputedAtUtc);
    }
}
