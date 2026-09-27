using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Reporting.Application;

// Module C - system performance (US-3.5.3-01..05). Technical metrics come from the telemetry source (Q-04); matching figures from FactMatch.

public sealed record GetSystemPerformanceQuery(DateOnly? From, DateOnly? To) : PerformanceRequest, IQuery<SystemPerformanceDto>;

public sealed record GetUsagePatternsQuery(DateOnly? From, DateOnly? To) : PerformanceRequest, IQuery<UsagePatternsDto>;

public sealed record GetPerformanceDashboardQuery(DateOnly? From, DateOnly? To) : PerformanceRequest, IQuery<PerformanceDashboardDto>;

public sealed record GetMetricHistoryQuery(string Metric, DateOnly? From, DateOnly? To, string? Granularity) : PerformanceRequest, IQuery<MetricHistoryDto>;

public sealed record ListAlertRulesQuery : PerformanceRequest, IQuery<IReadOnlyList<AlertRuleDto>>;

public sealed record ListAlertsQuery(int Take) : PerformanceRequest, IQuery<IReadOnlyList<AlertDto>>;

/// <summary>Creates a rule (Id null) or replaces an existing one (later save wins).</summary>
public sealed record ConfigurePerformanceAlertRuleCommand(Guid? Id, string Metric, string Comparator, decimal Threshold, int WindowMinutes, string Severity, bool Enabled)
    : PerformanceCommandRequest, ICommand<AlertRuleDto>;

/// <summary>Scheduled (leader-locked): evaluates every enabled rule over its window and raises alerts. Returns the number raised.</summary>
public sealed record EvaluatePerformanceAlertsCommand : ICommand<int>;

/// <summary>Scheduled: samples the telemetry source into FactSystemMetric. Returns the number of samples stored (0 = no data, degraded).</summary>
public sealed record SampleSystemMetricsCommand : ICommand<int>;

public sealed class GetSystemPerformanceValidator : AbstractValidator<GetSystemPerformanceQuery>
{
    public GetSystemPerformanceValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

public sealed class GetUsagePatternsValidator : AbstractValidator<GetUsagePatternsQuery>
{
    public GetUsagePatternsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

public sealed class GetPerformanceDashboardValidator : AbstractValidator<GetPerformanceDashboardQuery>
{
    public GetPerformanceDashboardValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

public sealed class GetMetricHistoryValidator : AbstractValidator<GetMetricHistoryQuery>
{
    public GetMetricHistoryValidator()
    {
        RuleFor(x => x.Metric).Must(PerformanceMetrics.IsKnown).WithErrorCode("VAL.Metric.Unknown");
        DateRangeRules.AddTo(this, x => x.From, x => x.To, x => x.Granularity);
    }
}

public sealed class ListAlertsValidator : AbstractValidator<ListAlertsQuery>
{
    public ListAlertsValidator() => RuleFor(x => x.Take).InclusiveBetween(1, 200).WithErrorCode("VAL.Take.OutOfRange");
}

/// <summary>ConfigurePerformanceAlertRuleValidator (handover 7): metric known, threshold numeric, window at least one minute.</summary>
public sealed class ConfigurePerformanceAlertRuleValidator : AbstractValidator<ConfigurePerformanceAlertRuleCommand>
{
    public ConfigurePerformanceAlertRuleValidator()
    {
        RuleFor(x => x.Metric).Must(PerformanceMetrics.IsKnown).WithErrorCode("VAL.Metric.Unknown");
        RuleFor(x => x.Comparator).Must(c => Enum.TryParse<AlertComparator>(c, true, out _)).WithErrorCode("VAL.Comparator.Invalid");
        RuleFor(x => x.Severity).Must(s => Enum.TryParse<AlertSeverity>(s, true, out _)).WithErrorCode("VAL.Severity.Invalid");
        RuleFor(x => x.WindowMinutes).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Window.TooShort");
        RuleFor(x => x.Threshold).InclusiveBetween(-1_000_000_000m, 1_000_000_000m).WithErrorCode("VAL.Threshold.OutOfRange");
    }
}

/// <summary>Reads and summarises matching + technical metrics; shared by the performance and dashboard queries.</summary>
internal sealed class PerformanceReader
{
    private readonly IAnalyticsQueryService _analytics;

    public PerformanceReader(IAnalyticsQueryService analytics) => _analytics = analytics;

    public async Task<SystemPerformanceDto> ReadAsync(DateRange range, CancellationToken ct)
    {
        var from = DateRanges.StartUtc(range.From);
        var to = DateRanges.EndUtc(range.To);
        var matches = await _analytics.MatchesAsync(from, to, 200_000, ct);
        var scores = matches.Where(m => m.Kind == "Score" && m.Score is not null).ToList();
        // Accuracy, precision, recall and satisfaction need labelled feedback that no BC publishes (handover Q-08): explicit "no data source".
        var unavailable = new FigureDto(true, null, 0, "NoLabelledFeedbackSource");
        var matching = new MatchingMetricsDto(scores.Count, scores.Count == 0 ? null : Math.Round(scores.Average(s => s.Score!.Value), 4),
            matches.Count(m => m.Kind == "Recommendation"), unavailable, unavailable, unavailable, unavailable);

        var samples = await _analytics.MetricsAsync(null, from, to, 200_000, ct);
        var technical = PerformanceMetrics.Known.Select(metric =>
        {
            var rows = samples.Where(s => s.Metric == metric).OrderBy(s => s.SampledAtUtc).ToList();
            return rows.Count == 0
                ? new MetricSummaryDto(metric, null, null, null, 0)
                : new MetricSummaryDto(metric, rows[^1].Value, Math.Round(rows.Average(r => r.Value), 4), rows.Max(r => r.Value), rows.Count);
        }).ToList();
        return new SystemPerformanceDto(range.From, range.To, samples.Count == 0, matching, technical);
    }
}

internal sealed class PerformanceHandlers :
    IQueryHandler<GetSystemPerformanceQuery, SystemPerformanceDto>,
    IQueryHandler<GetUsagePatternsQuery, UsagePatternsDto>,
    IQueryHandler<GetPerformanceDashboardQuery, PerformanceDashboardDto>,
    IQueryHandler<GetMetricHistoryQuery, MetricHistoryDto>,
    IQueryHandler<ListAlertRulesQuery, IReadOnlyList<AlertRuleDto>>,
    IQueryHandler<ListAlertsQuery, IReadOnlyList<AlertDto>>,
    ICommandHandler<ConfigurePerformanceAlertRuleCommand, AlertRuleDto>,
    ICommandHandler<EvaluatePerformanceAlertsCommand, int>,
    ICommandHandler<SampleSystemMetricsCommand, int>
{
    private readonly IReportAccessGuard _guard;
    private readonly IAnalyticsQueryService _analytics;
    private readonly IPerformanceAlertRuleRepository _rules;
    private readonly AlertEvaluator _evaluator;
    private readonly MetricsSampler _sampler;
    private readonly TimeProvider _clock;

    public PerformanceHandlers(IReportAccessGuard guard, IAnalyticsQueryService analytics, IPerformanceAlertRuleRepository rules, AlertEvaluator evaluator, MetricsSampler sampler,
        TimeProvider clock)
    {
        _guard = guard;
        _analytics = analytics;
        _rules = rules;
        _evaluator = evaluator;
        _sampler = sampler;
        _clock = clock;
    }

    private async Task<Error?> DeniedAsync(string name, CancellationToken ct) =>
        (await _guard.EnsureAsync(ReportCategory.SystemPerformance, name, ct)).Error;

    public async Task<Result<SystemPerformanceDto>> Handle(GetSystemPerformanceQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(GetSystemPerformanceQuery), ct) is { } denied)
        {
            return denied;
        }

        return await new PerformanceReader(_analytics).ReadAsync(DateRanges.Resolve(request.From, request.To, _clock), ct);
    }

    public async Task<Result<UsagePatternsDto>> Handle(GetUsagePatternsQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(GetUsagePatternsQuery), ct) is { } denied)
        {
            return denied;
        }

        return await UsageAsync(DateRanges.Resolve(request.From, request.To, _clock), ct);
    }

    private async Task<UsagePatternsDto> UsageAsync(DateRange range, CancellationToken ct)
    {
        var events = await _analytics.EventsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), 200_000, ct);
        var hours = events.GroupBy(e => e.OccurredAtUtc.Hour).OrderBy(g => g.Key).Select(g => new HourCountDto(g.Key, g.Count())).ToList();
        var features = events.GroupBy(e => e.ActivityType).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(10).Select(g => new ActivityCountDto(g.Key, g.Count())).ToList();
        var actors = events.Where(e => e.ActorKey != "anonymous").ToList();
        var days = Math.Max(1, range.To.DayNumber - range.From.DayNumber + 1);
        var dailyActive = actors.GroupBy(e => DateOnly.FromDateTime(e.OccurredAtUtc)).Sum(g => g.Select(e => e.ActorKey).Distinct().Count());
        return new UsagePatternsDto(range.From, range.To, hours, features, actors.Select(e => e.ActorKey).Distinct().LongCount(), Math.Round((decimal)dailyActive / days, 2), events.Count);
    }

    public async Task<Result<PerformanceDashboardDto>> Handle(GetPerformanceDashboardQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(GetPerformanceDashboardQuery), ct) is { } denied)
        {
            return denied;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var alerts = (await _rules.ListAlertsAsync(5, ct)).Select(ToDto).ToList();
        return new PerformanceDashboardDto(await new PerformanceReader(_analytics).ReadAsync(range, ct), await UsageAsync(range, ct), alerts);
    }

    public async Task<Result<MetricHistoryDto>> Handle(GetMetricHistoryQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(GetMetricHistoryQuery), ct) is { } denied)
        {
            return denied;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var granularity = (request.Granularity ?? "day").ToLowerInvariant();
        var rows = await _analytics.MetricsAsync(request.Metric, DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), 200_000, ct);
        var points = rows.GroupBy(r => Periods.Key(r.SampledAtUtc, granularity)).OrderBy(g => g.Key)
            .Select(g => new HistoryPointDto(g.Key, Math.Round(g.Average(r => r.Value), 4), g.Max(r => r.Value), g.Count())).ToList();
        return new MetricHistoryDto(request.Metric, range.From, range.To, granularity, points);
    }

    public async Task<Result<IReadOnlyList<AlertRuleDto>>> Handle(ListAlertRulesQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(ListAlertRulesQuery), ct) is { } denied)
        {
            return denied;
        }

        return (await _rules.ListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<Result<IReadOnlyList<AlertDto>>> Handle(ListAlertsQuery request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(ListAlertsQuery), ct) is { } denied)
        {
            return denied;
        }

        return (await _rules.ListAlertsAsync(request.Take, ct)).Select(ToDto).ToList();
    }

    public async Task<Result<AlertRuleDto>> Handle(ConfigurePerformanceAlertRuleCommand request, CancellationToken ct)
    {
        if (await DeniedAsync(nameof(ConfigurePerformanceAlertRuleCommand), ct) is { } denied)
        {
            return denied;
        }

        var comparator = Enum.Parse<AlertComparator>(request.Comparator, true);
        var severity = Enum.Parse<AlertSeverity>(request.Severity, true);
        var now = _clock.GetUtcNow().UtcDateTime;
        PerformanceAlertRule? rule = null;
        if (request.Id is { } id)
        {
            rule = await _rules.GetAsync(id, ct);
            if (rule is null)
            {
                return Error.NotFound(ReportingErrorCodes.NotFound, "The alert rule was not found.");
            }

            rule.Configure(request.Metric, comparator, request.Threshold, request.WindowMinutes, severity, request.Enabled, now);
        }
        else
        {
            rule = PerformanceAlertRule.Create(request.Metric, comparator, request.Threshold, request.WindowMinutes, severity, request.Enabled, now);
            _rules.Add(rule);
        }

        return ToDto(rule);
    }

    public async Task<Result<int>> Handle(EvaluatePerformanceAlertsCommand request, CancellationToken ct) => await _evaluator.EvaluateAsync(ct);

    public async Task<Result<int>> Handle(SampleSystemMetricsCommand request, CancellationToken ct) => await _sampler.SampleAsync(ct);

    internal static AlertRuleDto ToDto(PerformanceAlertRule r) => new(r.Id, r.Metric, r.Comparator.ToString(), r.Threshold, r.WindowMinutes, r.Severity.ToString(), r.IsEnabled);

    internal static AlertDto ToDto(PerformanceAlert a) => new(a.Id, a.RuleId, a.Metric, a.Severity.ToString(), a.Value, a.Threshold, a.RaisedAtUtc);
}

/// <summary>
/// Evaluates every enabled rule over its window (US-3.5.3-04). AC-02: a value in the normal range raises nothing. A rule that already alerted inside its own window
/// does not alert again (one alert per breach, not one per evaluation tick).
/// </summary>
public sealed class AlertEvaluator
{
    private readonly IPerformanceAlertRuleRepository _rules;
    private readonly IAnalyticsQueryService _analytics;
    private readonly TimeProvider _clock;

    public AlertEvaluator(IPerformanceAlertRuleRepository rules, IAnalyticsQueryService analytics, TimeProvider clock)
    {
        _rules = rules;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<int> EvaluateAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var raised = 0;
        foreach (var rule in await _rules.ListEnabledAsync(ct))
        {
            var window = now.AddMinutes(-rule.WindowMinutes);
            var samples = await _analytics.MetricsAsync(rule.Metric, window, now.AddSeconds(1), 10_000, ct);
            if (samples.Count == 0 || await _rules.HasAlertSinceAsync(rule.Id, window, ct))
            {
                continue;
            }

            var alert = rule.Evaluate(Math.Round(samples.Average(s => s.Value), 4), now);
            if (alert is not null)
            {
                _rules.AddAlert(alert);
                raised++;
            }
        }

        return raised;
    }
}

/// <summary>Pulls the current metric values from the telemetry source and stores them as samples. A source failure or an empty answer degrades to "no data" (Q-04).</summary>
public sealed class MetricsSampler
{
    private readonly IMetricsSource _source;
    private readonly IFactStore _facts;
    private readonly TimeProvider _clock;
    private readonly ILogger<MetricsSampler> _logger;

    public MetricsSampler(IMetricsSource source, IFactStore facts, TimeProvider clock, ILogger<MetricsSampler> logger)
    {
        _source = source;
        _facts = facts;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SampleAsync(CancellationToken ct)
    {
        IReadOnlyDictionary<string, decimal> values;
        try
        {
            values = await _source.SampleAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Telemetry source failed; system metrics degrade to no data");
            return 0;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var count = 0;
        foreach (var (metric, value) in values.Where(v => PerformanceMetrics.IsKnown(v.Key)))
        {
            _facts.Add(FactSystemMetric.Sample(metric, value, now, _source.GetType().Name));
            count++;
        }

        return count;
    }
}
