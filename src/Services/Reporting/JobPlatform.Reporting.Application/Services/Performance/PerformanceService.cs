using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Services.Performance;

/// <summary>Logic shared by the performance request handlers.</summary>
internal sealed class PerformanceService
{
    private readonly IReportAccessGuard _guard;
    private readonly IAnalyticsQueryService _analytics;

    public PerformanceService(IReportAccessGuard guard, IAnalyticsQueryService analytics)
    {
        _guard = guard;
        _analytics = analytics;
    }

    public async Task<Error?> DeniedAsync(string name, CancellationToken ct) =>
        (await _guard.EnsureAsync(ReportCategory.SystemPerformance, name, ct)).Error;

    public async Task<UsagePatternsDto> UsageAsync(DateRange range, CancellationToken ct)
    {
        var events = await _analytics.EventsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), 200_000, ct);
        var hours = events.GroupBy(e => e.OccurredAtUtc.Hour).OrderBy(g => g.Key).Select(g => new HourCountDto(g.Key, g.Count())).ToList();
        var features = events.GroupBy(e => e.ActivityType).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(10).Select(g => new ActivityCountDto(g.Key, g.Count())).ToList();
        var actors = events.Where(e => e.ActorKey != "anonymous").ToList();
        var days = Math.Max(1, range.To.DayNumber - range.From.DayNumber + 1);
        var dailyActive = actors.GroupBy(e => DateOnly.FromDateTime(e.OccurredAtUtc)).Sum(g => g.Select(e => e.ActorKey).Distinct().Count());
        return new UsagePatternsDto(range.From, range.To, hours, features, actors.Select(e => e.ActorKey).Distinct().LongCount(), Math.Round((decimal)dailyActive / days, 2), events.Count);
    }

    public static AlertRuleDto ToDto(PerformanceAlertRule r) => new(r.Id, r.Metric, r.Comparator.ToString(), r.Threshold, r.WindowMinutes, r.Severity.ToString(), r.IsEnabled);

    public static AlertDto ToDto(PerformanceAlert a) => new(a.Id, a.RuleId, a.Metric, a.Severity.ToString(), a.Value, a.Threshold, a.RaisedAtUtc);
}
