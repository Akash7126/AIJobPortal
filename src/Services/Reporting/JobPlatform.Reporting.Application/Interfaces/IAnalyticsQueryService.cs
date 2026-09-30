namespace JobPlatform.Reporting.Application.Interfaces;

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
