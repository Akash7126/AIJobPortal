using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application;

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

public sealed record EventRow(string SourceBc, string EventType, string ActivityType, string ActorType, string ActorKey, DateTime OccurredAtUtc);

public sealed record PostingRow(Guid Id, string Status, string? Category, string? Location, decimal? SalaryMin, decimal? SalaryMax, string Source, string? Title, DateTime FirstSeenAtUtc,
    DateTime? ClosedAtUtc);

public sealed record SkillRow(string Skill, string Side, DateTime OccurredAtUtc);

public sealed record RegistrationRow(string Milestone, string ActorType, string? Governorate, DateTime OccurredAtUtc);

public sealed record MatchRow(string Kind, decimal? Score, int? ItemCount, DateTime OccurredAtUtc);

public sealed record MetricRow(string Metric, decimal Value, DateTime SampledAtUtc);

public sealed record NotificationRow(string Channel, string Category, string Status, DateTime OccurredAtUtc);

public sealed record OutcomeRow(Guid JobPostingId, int Shortlisted, int FollowUps, decimal FitSum, int FitCount, DateTime FirstShortlistedAtUtc);

public sealed record DailyCount(DateOnly Day, string Metric, long Count);

/// <summary>Read side (foundation section 3.5): raw projections of the analytics store; the policies (insufficient data, small cells) are applied by the application.</summary>
public interface IAnalyticsQueryService
{
    Task<IReadOnlyList<DailyCount>> DailyCountsAsync(string metricPrefix, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<EventRow>> EventsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<PostingRow>> PostingsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<SkillRow>> SkillsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<MatchRow>> MatchesAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<MetricRow>> MetricsAsync(string? metric, DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationRow>> NotificationsAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);

    Task<IReadOnlyList<OutcomeRow>> OutcomesAsync(DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default);
}

/// <summary>Anti-corruption port to infrastructure telemetry (Prometheus / OTel; Q-04). Failure degrades to "no data".</summary>
public interface IMetricsSource
{
    /// <summary>Current value of every known metric the backend can supply. Empty when the backend has no data.</summary>
    Task<IReadOnlyDictionary<string, decimal>> SampleAsync(CancellationToken ct = default);
}

/// <summary>Anti-corruption port to BC-03's proposed <c>GET /internal/v1/sessions/active</c> (Q-03). Null = source unavailable (the dashboard degrades).</summary>
public interface ISessionSource
{
    Task<IReadOnlyList<ActiveSession>?> GetActiveAsync(CancellationToken ct = default);
}

/// <summary>What a Power BI publish returns.</summary>
public sealed record PowerBiPublication(string DatasetId, string ReportUrl);

/// <summary>Anti-corruption port to Microsoft Power BI (Q-10). Implementations must honour the cancellation token (the caller applies the 30 s timeout).</summary>
public interface IPowerBiExporter
{
    Task<PowerBiPublication> PublishAsync(string reportName, ReportTable table, CancellationToken ct = default);
}

/// <summary>Renders a tabular report to a file (CSV, Excel, PDF).</summary>
public interface IReportFileGenerator
{
    GeneratedFile Generate(string title, ReportTable table, ReportFormat format);
}

public sealed record GeneratedFile(string FileName, string ContentType, byte[] Content);

/// <summary>Signs and verifies expiring report links (the distribution event carries a link, never the content).</summary>
public interface IReportLinkSigner
{
    string Sign(Guid exportId, DateTime expiresUtc);

    bool Verify(Guid exportId, long expiresUnix, string signature, DateTime nowUtc);
}

/// <summary>Per-request category access decision (US-3.5.4-08): applies the rules of the caller's roles and records the decision.</summary>
public interface IReportAccessGuard
{
    Task<Result<Unit>> EnsureAsync(ReportCategory category, string requestName, CancellationToken ct = default);
}
