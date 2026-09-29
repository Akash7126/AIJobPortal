using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application;

/// <summary>Conversions between the API shapes and the domain types of the report framework.</summary>
public static class ReportMapping
{
    public static ReportDefinition ToDomain(ReportDefinitionDto dto) => new(
        Enum.TryParse<ReportDataSource>(dto.DataSource, true, out var source) ? source : (ReportDataSource)(-1),
        dto.Fields ?? Array.Empty<string>(),
        (dto.Filters ?? Array.Empty<ReportFilterDto>()).Select(f => new ReportFilter(f.Field, Enum.TryParse<FilterOperator>(f.Operator, true, out var op) ? op : (FilterOperator)(-1), f.Value)).ToList(),
        dto.GroupBy ?? Array.Empty<string>());

    public static ReportDefinitionDto ToDto(ReportDefinition d) =>
        new(d.DataSource.ToString(), d.Fields, d.Filters.Select(f => new ReportFilterDto(f.Field, f.Operator.ToString(), f.Value)).ToList(), d.GroupBy);

    public static TemplateDto ToDto(ReportTemplate t) => new(t.Id, t.Name, t.DataSource.ToString(),
        t.Parameters.Select(p => new TemplateParameterDto(p.Name, p.Type.ToString(), p.Min, p.Max, p.Default, p.Options)).ToList(), t.Revision, t.UpdatedAtUtc);

    public static TemplateParameter ToDomain(TemplateParameterDto p) =>
        new(p.Name, Enum.TryParse<ParameterType>(p.Type, true, out var type) ? type : (ParameterType)(-1), p.Min, p.Max, p.Default, p.Options);

    public static SavedReportDto ToDto(SavedReport r) => new(r.Id, r.Name, ToDto(r.Definition), r.CreatedAtUtc, r.IsArchived);

    /// <summary>A template runs with its data source's default fields; arguments named like a dimension filter it, "from" and "to" bound the date range.</summary>
    public static ReportDefinition DefinitionOf(ReportTemplate template, IReadOnlyDictionary<string, string> resolved)
    {
        var fields = template.DataSource switch
        {
            ReportDataSource.Activity => new[] { "activityType", "eventType", "events", "distinctActors" },
            ReportDataSource.Employment => new[] { "category", "location", "status", "postings", "avgSalary" },
            _ => new[] { "metric", "samples", "avgValue", "minValue", "maxValue" }
        };
        var filters = resolved.Where(kv => ReportCatalog.Find(template.DataSource, kv.Key) is { Kind: FieldKind.Dimension })
            .Select(kv => new ReportFilter(kv.Key, FilterOperator.Eq, kv.Value)).ToList();
        return new ReportDefinition(template.DataSource, fields, filters, Array.Empty<string>());
    }
}
