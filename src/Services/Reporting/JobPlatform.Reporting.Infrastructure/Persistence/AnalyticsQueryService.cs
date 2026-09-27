using JobPlatform.Reporting.Application;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

/// <summary>Read side: AsNoTracking projections of the analytics store straight into row records, never through aggregates (foundation section 3.5).</summary>
internal sealed class AnalyticsQueryService : IAnalyticsQueryService
{
    private readonly ReportingDbContext _db;

    public AnalyticsQueryService(ReportingDbContext db) => _db = db;

    public async Task<IReadOnlyList<DailyCount>> DailyCountsAsync(string metricPrefix, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await _db.AggDaily.AsNoTracking().Where(a => a.Day >= from && a.Day <= to && a.Metric.StartsWith(metricPrefix))
            .Select(a => new DailyCount(a.Day, a.Metric, a.Count)).ToListAsync(ct);

    public async Task<IReadOnlyList<EventRow>> EventsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactEvents.AsNoTracking().Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc).OrderBy(e => e.OccurredAtUtc).Take(max)
            .Select(e => new EventRow(e.SourceBc, e.EventType, e.ActivityType, e.ActorType, e.ActorKey, e.OccurredAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<PostingRow>> PostingsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactJobPostings.AsNoTracking().Where(p => p.FirstSeenAtUtc >= fromUtc && p.FirstSeenAtUtc < toUtc).OrderBy(p => p.FirstSeenAtUtc).Take(max)
            .Select(p => new PostingRow(p.Id, p.Status, p.Category, p.Location, p.SalaryMin, p.SalaryMax, p.Source, p.Title, p.FirstSeenAtUtc, p.ClosedAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<SkillRow>> SkillsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactSkillDemand.AsNoTracking().Where(s => s.OccurredAtUtc >= fromUtc && s.OccurredAtUtc < toUtc).OrderBy(s => s.OccurredAtUtc).Take(max)
            .Select(s => new SkillRow(s.Skill, s.Side, s.OccurredAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactRegistrations.AsNoTracking().Where(r => r.OccurredAtUtc >= fromUtc && r.OccurredAtUtc < toUtc).OrderBy(r => r.OccurredAtUtc).Take(max)
            .Select(r => new RegistrationRow(r.Milestone, r.ActorType, r.Governorate, r.OccurredAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<MatchRow>> MatchesAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactMatches.AsNoTracking().Where(m => m.OccurredAtUtc >= fromUtc && m.OccurredAtUtc < toUtc).OrderBy(m => m.OccurredAtUtc).Take(max)
            .Select(m => new MatchRow(m.Kind, m.Score, m.ItemCount, m.OccurredAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<MetricRow>> MetricsAsync(string? metric, DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactSystemMetrics.AsNoTracking().Where(m => (metric == null || m.Metric == metric) && m.SampledAtUtc >= fromUtc && m.SampledAtUtc < toUtc)
            .OrderBy(m => m.SampledAtUtc).Take(max).Select(m => new MetricRow(m.Metric, m.Value, m.SampledAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<NotificationRow>> NotificationsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactNotifications.AsNoTracking().Where(n => n.OccurredAtUtc >= fromUtc && n.OccurredAtUtc < toUtc).OrderBy(n => n.OccurredAtUtc).Take(max)
            .Select(n => new NotificationRow(n.Channel, n.Category, n.Status, n.OccurredAtUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<OutcomeRow>> OutcomesAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default) =>
        await _db.FactOutcomes.AsNoTracking().Where(o => o.FirstShortlistedAtUtc >= fromUtc && o.FirstShortlistedAtUtc < toUtc).OrderBy(o => o.FirstShortlistedAtUtc).Take(max)
            .Select(o => new OutcomeRow(o.Id, o.Shortlisted, o.FollowUps, o.FitSum, o.FitCount, o.FirstShortlistedAtUtc)).ToListAsync(ct);
}
