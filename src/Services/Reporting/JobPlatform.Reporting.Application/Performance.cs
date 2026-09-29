using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Domain;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Reporting.Application;

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
