using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Reporting.Application;

/// <summary>
/// Runs a report to a result (US-3.5.4-01). The built-in target computes it locally. The Power BI target publishes the result with a per-attempt timeout
/// (30 s by default) and up to 3 attempts; when every attempt fails the built-in view is returned with the warning code E-CRG-UPSTREAM-TIMEOUT (RP.PowerBI.UPSTREAM_TIMEOUT).
/// </summary>
public sealed class ReportRunner
{
    private readonly ReportEngine _engine;
    private readonly IPowerBiExporter _powerBi;
    private readonly ReportingOptionsAccessor _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportRunner> _logger;

    public ReportRunner(ReportEngine engine, IPowerBiExporter powerBi, ReportingOptionsAccessor options, TimeProvider clock, ILogger<ReportRunner> logger)
    {
        _engine = engine;
        _powerBi = powerBi;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ReportResultDto> RunAsync(string title, ReportDefinition definition, IReadOnlyDictionary<string, string>? arguments, ReportTarget target, CancellationToken ct)
    {
        var table = await _engine.RunAsync(definition, arguments, ct);
        if (target == ReportTarget.Builtin)
        {
            return new ReportResultDto("builtin", false, null, null, title, table);
        }

        var attempts = Math.Max(1, _options.Value.PowerBiAttempts);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var timeout = new CancellationTokenSource(_options.Value.PowerBiTimeout, _clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                var published = await _powerBi.PublishAsync(title, table, linked.Token);
                return new ReportResultDto("powerbi", false, null, published.ReportUrl, title, table);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Power BI publish attempt {Attempt} of {Attempts} failed", attempt, attempts);
            }
        }

        return new ReportResultDto("builtin", true, ReportingErrorCodes.CustomUpstreamTimeout, null, title, table);
    }
}

/// <summary>Turns a tabular result into the requested view (US-3.5.4-03): tabular, chart specification or visualisation specification.</summary>
public sealed class ReportViewBuilder
{
    public const string Tabular = "tabular";
    public const string Chart = "chart";
    public const string Visual = "visual";

    public static readonly IReadOnlyList<string> Formats = new[] { Tabular, Chart, Visual };

    public static bool IsKnown(string? format) => format is not null && Formats.Contains(format, StringComparer.OrdinalIgnoreCase);

    public ReportViewDto Build(string title, ReportTable table, string format)
    {
        format = format.ToLowerInvariant();
        if (format == Tabular)
        {
            return new ReportViewDto(format, title, table, null, null, null, null);
        }

        var dimensionIndexes = table.Columns.Select((c, i) => (c, i)).Where(x => x.c.Kind == "dimension").Select(x => x.i).ToList();
        var measureIndexes = table.Columns.Select((c, i) => (c, i)).Where(x => x.c.Kind == "measure").Select(x => x.i).ToList();
        var labels = table.Rows.Select(r => string.Join(" / ", dimensionIndexes.Select(i => ReportFileGenerator.Format(r[i])))).ToList();
        var series = measureIndexes.Select(i => new ChartSeriesDto(table.Columns[i].Name, table.Rows.Select(r => r[i] is null ? (decimal?)null : Convert.ToDecimal(r[i])).ToList())).ToList();
        var chartType = dimensionIndexes.Count == 1 && table.Columns[dimensionIndexes[0]].Name is "day" or "month" ? "line" : "bar";
        if (format == Chart)
        {
            return new ReportViewDto(format, title, null, labels, series, chartType, null);
        }

        var highlights = series.Select(s => new KeyValuePair<string, decimal?>(s.Name, s.Values.Any(v => v is not null) ? s.Values.Where(v => v is not null).Sum(v => v!.Value) : null)).ToList();
        highlights.Insert(0, new KeyValuePair<string, decimal?>("rows", table.Rows.Count));
        return new ReportViewDto(format, title, null, labels, series, chartType, highlights);
    }
}
