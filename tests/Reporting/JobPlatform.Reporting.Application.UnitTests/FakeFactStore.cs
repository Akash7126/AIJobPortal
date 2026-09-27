using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.UnitTests;

/// <summary>In-memory IFactStore so ingestion/projector handlers are tested against real state.</summary>
public sealed class FakeFactStore : IFactStore
{
    public List<FactEvent> Events { get; } = new();
    public List<FactSkillDemand> Skills { get; } = new();
    public List<FactMatch> Matches { get; } = new();
    public List<FactRegistration> Registrations { get; } = new();
    public List<FactNotification> Notifications { get; } = new();
    public List<FactSystemMetric> Metrics { get; } = new();
    public Dictionary<Guid, FactJobPosting> Postings { get; } = new();
    public Dictionary<Guid, FactOutcome> Outcomes { get; } = new();
    public Dictionary<(DateOnly, string), long> Daily { get; } = new();

    public Task<bool> EventExistsAsync(Guid messageId, CancellationToken ct = default) => Task.FromResult(Events.Any(e => e.MessageId == messageId));

    public void Add(FactEvent fact) => Events.Add(fact);

    public Task<FactJobPosting> GetOrOpenPostingAsync(Guid jobPostingId, DateTime atUtc, CancellationToken ct = default)
    {
        if (!Postings.TryGetValue(jobPostingId, out var posting))
        {
            posting = FactJobPosting.Open(jobPostingId, atUtc);
            Postings[jobPostingId] = posting;
        }

        return Task.FromResult(posting);
    }

    public Task<bool> SkillFactExistsAsync(string skill, string side, Guid subjectId, CancellationToken ct = default) =>
        Task.FromResult(Skills.Any(s => s.Skill == skill && s.Side == side && s.SubjectId == subjectId));

    public void Add(FactSkillDemand fact) => Skills.Add(fact);

    public Task<bool> MatchExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Matches.Any(m => m.Id == id));

    public void Add(FactMatch fact) => Matches.Add(fact);

    public void Add(FactRegistration fact) => Registrations.Add(fact);

    public Task<FactNotification?> GetNotificationAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Notifications.FirstOrDefault(n => n.Id == id));

    public void Add(FactNotification fact) => Notifications.Add(fact);

    public void Add(FactSystemMetric fact) => Metrics.Add(fact);

    public Task<FactOutcome> GetOrOpenOutcomeAsync(Guid jobPostingId, DateTime atUtc, CancellationToken ct = default)
    {
        if (!Outcomes.TryGetValue(jobPostingId, out var outcome))
        {
            outcome = FactOutcome.ForPosting(jobPostingId, atUtc);
            Outcomes[jobPostingId] = outcome;
        }

        return Task.FromResult(outcome);
    }

    public Task AddToDailyAsync(DateOnly day, string metric, long by, CancellationToken ct = default)
    {
        Daily[(day, metric)] = Daily.GetValueOrDefault((day, metric)) + by;
        return Task.CompletedTask;
    }

    public Task<int> DeleteEventsBeforeAsync(DateTime cutoffUtc, int take, CancellationToken ct = default)
    {
        var doomed = Events.Where(e => e.OccurredAtUtc < cutoffUtc).Take(take).ToList();
        foreach (var e in doomed)
        {
            Events.Remove(e);
        }

        return Task.FromResult(doomed.Count);
    }

    public Task<int> DeleteHistoryBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) => Task.FromResult(0);

    public Task<int> RebuildRollupsAsync(CancellationToken ct = default) => Task.FromResult(0);
}
