using System.Text.Json;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;

namespace JobPlatform.Reporting.Application;

/// <summary>Produces the table behind an export or scheduled run from its reference (template, saved report or labor-market report) and renders it to a file.</summary>
public sealed class ExportContentBuilder
{
    private readonly IReportTemplateRepository _templates;
    private readonly ISavedReportRepository _saved;
    private readonly ILaborMarketReportRepository _labor;
    private readonly LaborMarketReportBuilder _laborBuilder;
    private readonly ReportEngine _engine;
    private readonly IReportFileGenerator _files;
    private readonly TimeProvider _clock;

    public ExportContentBuilder(IReportTemplateRepository templates, ISavedReportRepository saved, ILaborMarketReportRepository labor, LaborMarketReportBuilder laborBuilder,
        ReportEngine engine, IReportFileGenerator files, TimeProvider clock)
    {
        _templates = templates;
        _saved = saved;
        _labor = labor;
        _laborBuilder = laborBuilder;
        _engine = engine;
        _files = files;
        _clock = clock;
    }

    /// <summary>Returns null when the referenced report no longer exists.</summary>
    public async Task<GeneratedFile?> BuildAsync(ReportRefKind kind, Guid? refId, IReadOnlyDictionary<string, string> parameters, ReportFormat format, CancellationToken ct)
    {
        string title;
        ReportTable table;
        switch (kind)
        {
            case ReportRefKind.Template:
                var template = refId is null ? null : await _templates.GetAsync(refId.Value, ct);
                if (template is null)
                {
                    return null;
                }

                var resolved = template.ResolveArguments(parameters);
                title = template.Name;
                table = await _engine.RunAsync(ReportMapping.DefinitionOf(template, resolved), resolved, ct);
                break;
            case ReportRefKind.SavedReport:
                var saved = refId is null ? null : await _saved.GetAsync(refId.Value, ct);
                if (saved is null)
                {
                    return null;
                }

                title = saved.Name;
                table = await _engine.RunAsync(saved.Definition, parameters, ct);
                break;
            default:
                var now = _clock.GetUtcNow().UtcDateTime;
                var period = parameters.TryGetValue("period", out var p) ? p : LaborMarketReport.PeriodOf(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1));
                var existing = await _labor.GetByPeriodAsync(period, ct);
                title = "LaborMarket-" + period;
                table = Flatten(existing?.ContentJson ?? await _laborBuilder.BuildJsonAsync(period, ct));
                break;
        }

        return _files.Generate(title, table, format);
    }

    /// <summary>Flattens the labor-market JSON to (section, name, value) rows.</summary>
    public static ReportTable Flatten(string json)
    {
        var rows = new List<IReadOnlyList<object?>>();
        using var doc = JsonDocument.Parse(json);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    rows.AddRange(property.Value.EnumerateObject().Select(o => (IReadOnlyList<object?>)new object?[] { property.Name, o.Name, Scalar(o.Value) }));
                    break;
                case JsonValueKind.Array:
                    rows.AddRange(property.Value.EnumerateArray().Select(item => (IReadOnlyList<object?>)new object?[]
                    {
                        property.Name, item.TryGetProperty("name", out var n) ? n.GetString() : null, item.TryGetProperty("count", out var c) ? c.GetInt64() : null
                    }));
                    break;
                default:
                    rows.Add(new object?[] { "summary", property.Name, Scalar(property.Value) });
                    break;
            }
        }

        return new ReportTable(new[] { new ReportColumn("section", "dimension"), new ReportColumn("name", "dimension"), new ReportColumn("value", "measure") }, rows);
    }

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDecimal(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
