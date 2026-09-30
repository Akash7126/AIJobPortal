using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

/// <summary>Write side of the analytics store. Rows added earlier in the same unit of work are found through the change tracker before the database is asked.</summary>
internal sealed class FactStore : IFactStore
{
    private readonly ReportingDbContext _db;

    public FactStore(ReportingDbContext db) => _db = db;

    public Task<bool> EventExistsAsync(Guid messageId, CancellationToken ct = default) => _db.FactEvents.AnyAsync(e => e.MessageId == messageId, ct);

    public void Add(FactEvent fact) => _db.FactEvents.Add(fact);

    public async Task<FactJobPosting> GetOrOpenPostingAsync(Guid jobPostingId, DateTime atUtc, CancellationToken ct = default)
    {
        var posting = _db.FactJobPostings.Local.FirstOrDefault(p => p.Id == jobPostingId) ?? await _db.FactJobPostings.FirstOrDefaultAsync(p => p.Id == jobPostingId, ct);
        if (posting is null)
        {
            posting = FactJobPosting.Open(jobPostingId, atUtc);
            _db.FactJobPostings.Add(posting);
        }

        return posting;
    }

    public async Task<bool> SkillFactExistsAsync(string skill, string side, Guid subjectId, CancellationToken ct = default) =>
        _db.FactSkillDemand.Local.Any(f => f.Skill == skill && f.Side == side && f.SubjectId == subjectId)
        || await _db.FactSkillDemand.AnyAsync(f => f.Skill == skill && f.Side == side && f.SubjectId == subjectId, ct);

    public void Add(FactSkillDemand fact) => _db.FactSkillDemand.Add(fact);

    public async Task<bool> MatchExistsAsync(Guid id, CancellationToken ct = default) => _db.FactMatches.Local.Any(m => m.Id == id) || await _db.FactMatches.AnyAsync(m => m.Id == id, ct);

    public void Add(FactMatch fact) => _db.FactMatches.Add(fact);

    public void Add(FactRegistration fact) => _db.FactRegistrations.Add(fact);

    public async Task<FactNotification?> GetNotificationAsync(Guid id, CancellationToken ct = default) =>
        _db.FactNotifications.Local.FirstOrDefault(n => n.Id == id) ?? await _db.FactNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public void Add(FactNotification fact) => _db.FactNotifications.Add(fact);

    public void Add(FactSystemMetric fact) => _db.FactSystemMetrics.Add(fact);

    public async Task<FactOutcome> GetOrOpenOutcomeAsync(Guid jobPostingId, DateTime atUtc, CancellationToken ct = default)
    {
        var outcome = _db.FactOutcomes.Local.FirstOrDefault(o => o.Id == jobPostingId) ?? await _db.FactOutcomes.FirstOrDefaultAsync(o => o.Id == jobPostingId, ct);
        if (outcome is null)
        {
            outcome = FactOutcome.ForPosting(jobPostingId, atUtc);
            _db.FactOutcomes.Add(outcome);
        }

        return outcome;
    }

    public async Task AddToDailyAsync(DateOnly day, string metric, long by, CancellationToken ct = default)
    {
        var row = _db.AggDaily.Local.FirstOrDefault(a => a.Day == day && a.Metric == metric) ?? await _db.AggDaily.FirstOrDefaultAsync(a => a.Day == day && a.Metric == metric, ct);
        if (row is null)
        {
            _db.AggDaily.Add(AggDaily.For(day, metric, by));
        }
        else
        {
            row.Add(by);
        }
    }

    public async Task<int> DeleteEventsBeforeAsync(DateTime cutoffUtc, int take, CancellationToken ct = default)
    {
        var ids = await _db.FactEvents.Where(e => e.OccurredAtUtc < cutoffUtc).OrderBy(e => e.OccurredAtUtc).Select(e => e.Id).Take(take).ToListAsync(ct);
        return ids.Count == 0 ? 0 : await _db.FactEvents.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<int> DeleteHistoryBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var deleted = await _db.FactSystemMetrics.Where(m => m.SampledAtUtc < cutoffUtc).ExecuteDeleteAsync(ct);
        deleted += await _db.FactOutcomes.Where(o => o.LastUpdatedAtUtc < cutoffUtc).ExecuteDeleteAsync(ct);
        deleted += await _db.FactMatches.Where(m => m.OccurredAtUtc < cutoffUtc).ExecuteDeleteAsync(ct);
        return deleted;
    }

    public async Task<int> RebuildRollupsAsync(CancellationToken ct = default)
    {
        await _db.AggDaily.ExecuteDeleteAsync(ct);
        _db.ChangeTracker.Clear();
        var groups = await _db.FactEvents.AsNoTracking().GroupBy(e => new { e.OccurredAtUtc.Date, e.EventType, e.ActivityType })
            .Select(g => new { g.Key.Date, g.Key.EventType, g.Key.ActivityType, Count = g.Count() }).ToListAsync(ct);
        var counters = new Dictionary<(DateOnly, string), long>();
        foreach (var g in groups)
        {
            var day = DateOnly.FromDateTime(g.Date);
            counters[(day, "event." + g.EventType)] = counters.GetValueOrDefault((day, "event." + g.EventType)) + g.Count;
            counters[(day, "activity." + g.ActivityType)] = counters.GetValueOrDefault((day, "activity." + g.ActivityType)) + g.Count;
        }

        foreach (var ((day, metric), count) in counters)
        {
            _db.AggDaily.Add(AggDaily.For(day, metric, count));
        }

        return counters.Count;
    }
}
