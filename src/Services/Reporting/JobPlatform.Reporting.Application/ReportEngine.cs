using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application;

/// <summary>
/// Runs a report definition over the analytics store (US-3.5.4-01/-06): builds raw rows of the data source, applies the typed filters, groups by the chosen
/// dimensions, aggregates the measures and suppresses small cells (activity and employment groups below MinCell are counted, not shown).
/// </summary>
public sealed class ReportEngine
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly ReportingOptionsAccessor _options;
    private readonly TimeProvider _clock;

    public ReportEngine(IAnalyticsQueryService analytics, StatisticsPolicy policy, ReportingOptionsAccessor options, TimeProvider clock)
    {
        _analytics = analytics;
        _policy = policy;
        _options = options;
        _clock = clock;
    }

    public async Task<ReportTable> RunAsync(ReportDefinition definition, IReadOnlyDictionary<string, string>? arguments, CancellationToken ct)
    {
        var def = definition.ValidateAndNormalise();
        var range = Range(arguments);
        var rows = await LoadRowsAsync(def.DataSource, range, ct);
        return Aggregate(def, rows);
    }

    private DateRange Range(IReadOnlyDictionary<string, string>? arguments)
    {
        DateOnly? Parse(string key) => arguments is not null && arguments.TryGetValue(key, out var v) && DateOnly.TryParse(v, out var d) ? d : null;
        return DateRanges.Resolve(Parse("from"), Parse("to"), _clock);
    }

    private async Task<List<Dictionary<string, object?>>> LoadRowsAsync(ReportDataSource source, DateRange range, CancellationToken ct)
    {
        var from = DateRanges.StartUtc(range.From);
        var to = DateRanges.EndUtc(range.To);
        var max = _options.Value.MaxReportRows;
        switch (source)
        {
            case ReportDataSource.Activity:
                return (await _analytics.EventsAsync(from, to, max, ct)).Select(e => new Dictionary<string, object?>
                {
                    ["day"] = Periods.Key(e.OccurredAtUtc, "day"), ["month"] = Periods.Key(e.OccurredAtUtc, "month"), ["eventType"] = e.EventType, ["sourceBc"] = e.SourceBc,
                    ["activityType"] = e.ActivityType, ["actorType"] = e.ActorType, ["actorKey"] = e.ActorKey, ["one"] = 1m
                }).ToList();
            case ReportDataSource.Employment:
                return (await _analytics.PostingsAsync(from, to, max, ct)).Select(p => new Dictionary<string, object?>
                {
                    ["month"] = Periods.Key(p.FirstSeenAtUtc, "month"), ["category"] = StatisticsCalculator.Bucket(p.Category), ["location"] = StatisticsCalculator.Bucket(p.Location),
                    ["status"] = p.Status, ["source"] = p.Source,
                    ["salary"] = p.SalaryMin is null && p.SalaryMax is null ? null : (object)StatisticsCalculator.Midpoint(p.SalaryMin, p.SalaryMax), ["one"] = 1m
                }).ToList();
            default:
                return (await _analytics.MetricsAsync(null, from, to, max, ct)).Select(m => new Dictionary<string, object?>
                {
                    ["day"] = Periods.Key(m.SampledAtUtc, "day"), ["metric"] = m.Metric, ["value"] = m.Value, ["one"] = 1m
                }).ToList();
        }
    }

    private ReportTable Aggregate(ReportDefinition def, List<Dictionary<string, object?>> rows)
    {
        var fields = def.Fields.Select(f => ReportCatalog.Find(def.DataSource, f)!).ToList();
        var filtered = rows.Where(r => def.Filters.All(f => Matches(r, f))).ToList();
        var suppress = def.DataSource != ReportDataSource.Performance;
        var suppressed = 0;
        var output = new List<IReadOnlyList<object?>>();
        var hasMeasures = fields.Any(f => f.Kind == FieldKind.Measure);

        var groups = hasMeasures
            ? filtered.GroupBy(r => string.Join('\u001f', def.GroupBy.Select(g => Convert.ToString(r[ReportCatalog.Find(def.DataSource, g)!.SourceColumn]))))
            : filtered.GroupBy(r => string.Join('\u001f', fields.Select(f => Convert.ToString(r[f.SourceColumn]))));
        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (suppress && _policy.IsSuppressed(group.Count()))
            {
                suppressed++;
                continue;
            }

            var first = group.First();
            output.Add(fields.Select(f => f.Kind == FieldKind.Dimension ? first[f.SourceColumn] : Measure(f, group)).ToList());
        }

        var max = _options.Value.MaxReportRows;
        return new ReportTable(fields.Select(f => new ReportColumn(f.Name, f.Kind == FieldKind.Dimension ? "dimension" : "measure")).ToList(),
            output.Take(max).ToList(), output.Count > max, suppressed);
    }

    private static object? Measure(ReportField field, IEnumerable<Dictionary<string, object?>> group)
    {
        var values = group.Select(r => r[field.SourceColumn]).Where(v => v is not null).ToList();
        return field.Aggregation switch
        {
            Aggregation.Count => (long)group.Count(),
            Aggregation.DistinctCount => (long)values.Select(v => Convert.ToString(v)).Distinct().Count(),
            Aggregation.Sum => values.Sum(Convert.ToDecimal),
            Aggregation.Average => values.Count == 0 ? null : Math.Round(values.Average(Convert.ToDecimal), 2),
            Aggregation.Min => values.Count == 0 ? null : values.Min(Convert.ToDecimal),
            Aggregation.Max => values.Count == 0 ? null : values.Max(Convert.ToDecimal),
            _ => null
        };
    }

    private static bool Matches(Dictionary<string, object?> row, ReportFilter filter)
    {
        var field = row.Keys.FirstOrDefault(k => k.Equals(filter.Field, StringComparison.OrdinalIgnoreCase)) ?? filter.Field;
        var value = row.TryGetValue(field, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
        return filter.Operator switch
        {
            FilterOperator.Eq => value.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Neq => !value.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.In => filter.Value.Split(',', StringSplitOptions.TrimEntries).Contains(value, StringComparer.OrdinalIgnoreCase),
            FilterOperator.Gte => string.CompareOrdinal(value, filter.Value) >= 0,
            _ => string.CompareOrdinal(value, filter.Value) <= 0
        };
    }
}
