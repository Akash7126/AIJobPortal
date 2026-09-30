using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Write side of the analytics store (facts, dimensions, rollups). Ingestion never writes tables directly (foundation 9.4).</summary>
public interface IFactStore
{
    Task<bool> EventExistsAsync(Guid messageId, CancellationToken ct = default);

    void Add(FactEvent fact);

    Task<FactJobPosting> GetOrOpenPostingAsync(Guid jobPostingId, DateTime atUtc, CancellationToken ct = default);

    Task<bool> SkillFactExistsAsync(string skill, string side, Guid subjectId, CancellationToken ct = default);

    void Add(FactSkillDemand fact);

    Task<bool> MatchExistsAsync(Guid id, CancellationToken ct = default);

    void Add(FactMatch fact);

    void Add(FactRegistration fact);

    Task<FactNotification?> GetNotificationAsync(Guid id, CancellationToken ct = default);

    void Add(FactNotification fact);

    void Add(FactSystemMetric fact);

    Task<FactOutcome> GetOrOpenOutcomeAsync(Guid jobPostingId, DateTime atUtc, CancellationToken ct = default);

    /// <summary>Adds to the daily counter (created when missing).</summary>
    Task AddToDailyAsync(DateOnly day, string metric, long by, CancellationToken ct = default);

    /// <summary>Deletes FactEvent rows older than the cutoff (retention job, AC-04). Rollups are kept. Returns the number deleted.</summary>
    Task<int> DeleteEventsBeforeAsync(DateTime cutoffUtc, int take, CancellationToken ct = default);

    /// <summary>Deletes metric samples, outcomes and match facts older than the 12-month history window.</summary>
    Task<int> DeleteHistoryBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>Recomputes every daily counter from FactEvent (ops command). Returns the number of counters written.</summary>
    Task<int> RebuildRollupsAsync(CancellationToken ct = default);
}
