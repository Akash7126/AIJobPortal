using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

public enum FieldKind
{
    Dimension,
    Measure
}

/// <summary>Aggregation of a measure over the raw rows a data source yields.</summary>
public enum Aggregation
{
    None,
    Sum,
    Average,
    Min,
    Max,
    DistinctCount,
    Count
}

/// <summary>A field the report builder may offer (whitelist, US-3.5.4-06). Source column names are the raw-row keys of the analytics store.</summary>
public sealed record ReportField(string Name, FieldKind Kind, Aggregation Aggregation, string SourceColumn, bool IsDate = false);

/// <summary>The field whitelist per data source and the report category that guards each one (handover 3.9, 7).</summary>
public static class ReportCatalog
{
    public static ReportCategory CategoryOf(ReportDataSource source) => source switch
    {
        ReportDataSource.Activity => ReportCategory.ActivityLogs,
        ReportDataSource.Employment => ReportCategory.EmploymentStatistics,
        _ => ReportCategory.SystemPerformance
    };

    public static IReadOnlyList<ReportField> FieldsOf(ReportDataSource source) => source switch
    {
        ReportDataSource.Activity => new ReportField[]
        {
            new("day", FieldKind.Dimension, Aggregation.None, "day", true),
            new("month", FieldKind.Dimension, Aggregation.None, "month", true),
            new("eventType", FieldKind.Dimension, Aggregation.None, "eventType"),
            new("sourceBc", FieldKind.Dimension, Aggregation.None, "sourceBc"),
            new("activityType", FieldKind.Dimension, Aggregation.None, "activityType"),
            new("actorType", FieldKind.Dimension, Aggregation.None, "actorType"),
            new("events", FieldKind.Measure, Aggregation.Count, "one"),
            new("distinctActors", FieldKind.Measure, Aggregation.DistinctCount, "actorKey")
        },
        ReportDataSource.Employment => new ReportField[]
        {
            new("month", FieldKind.Dimension, Aggregation.None, "month", true),
            new("category", FieldKind.Dimension, Aggregation.None, "category"),
            new("location", FieldKind.Dimension, Aggregation.None, "location"),
            new("status", FieldKind.Dimension, Aggregation.None, "status"),
            new("source", FieldKind.Dimension, Aggregation.None, "source"),
            new("postings", FieldKind.Measure, Aggregation.Count, "one"),
            new("avgSalary", FieldKind.Measure, Aggregation.Average, "salary"),
            new("minSalary", FieldKind.Measure, Aggregation.Min, "salary"),
            new("maxSalary", FieldKind.Measure, Aggregation.Max, "salary")
        },
        _ => new ReportField[]
        {
            new("day", FieldKind.Dimension, Aggregation.None, "day", true),
            new("metric", FieldKind.Dimension, Aggregation.None, "metric"),
            new("samples", FieldKind.Measure, Aggregation.Count, "one"),
            new("avgValue", FieldKind.Measure, Aggregation.Average, "value"),
            new("minValue", FieldKind.Measure, Aggregation.Min, "value"),
            new("maxValue", FieldKind.Measure, Aggregation.Max, "value")
        }
    };

    public static ReportField? Find(ReportDataSource source, string name) =>
        FieldsOf(source).FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public enum FilterOperator
{
    Eq,
    Neq,
    In,
    Gte,
    Lte
}

/// <param name="Value">A single value, or a comma separated list for <see cref="FilterOperator.In"/>. Dates are ISO (yyyy-MM-dd or yyyy-MM).</param>
public sealed record ReportFilter(string Field, FilterOperator Operator, string Value);

/// <summary>Value object produced by the Report Builder (US-3.5.4-06): data source, selected fields, typed filters and grouping.</summary>
public sealed record ReportDefinition(ReportDataSource DataSource, IReadOnlyList<string> Fields, IReadOnlyList<ReportFilter> Filters, IReadOnlyList<string> GroupBy)
{
    public const int MaxFields = 30;

    /// <summary>Validates against the whitelist and returns the definition with canonical field names and the default grouping (every selected dimension).</summary>
    public ReportDefinition ValidateAndNormalise()
    {
        Guard.Ensure(Enum.IsDefined(DataSource), ReportingRuleCodes.InvalidDefinition, "Unknown data source.", ReportingErrorCodes.CustomForbidden, BusinessRuleKind.InvalidInput);
        Guard.Ensure(Fields is { Count: > 0 } && Fields.Count <= MaxFields, ReportingRuleCodes.InvalidDefinition, $"A report needs between 1 and {MaxFields} fields.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);

        var fields = new List<ReportField>();
        foreach (var name in Fields)
        {
            var field = ReportCatalog.Find(DataSource, name ?? string.Empty);
            Guard.Ensure(field is not null, ReportingRuleCodes.InvalidDefinition, $"Field '{name}' is not available for {DataSource}.", ReportingErrorCodes.CustomInvalidField,
                BusinessRuleKind.InvalidInput);
            Guard.Ensure(fields.All(f => f.Name != field!.Name), ReportingRuleCodes.InvalidDefinition, $"Field '{name}' is selected twice.", ReportingErrorCodes.CustomInvalidField,
                BusinessRuleKind.InvalidInput);
            fields.Add(field!);
        }

        var dimensions = fields.Where(f => f.Kind == FieldKind.Dimension).Select(f => f.Name).ToList();
        var hasMeasures = fields.Any(f => f.Kind == FieldKind.Measure);
        var groupBy = new List<string>();
        foreach (var name in GroupBy ?? Array.Empty<string>())
        {
            var field = fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Guard.Ensure(field is { Kind: FieldKind.Dimension }, ReportingRuleCodes.InvalidDefinition, $"Cannot group by '{name}': it must be a selected dimension.",
                ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
            groupBy.Add(field!.Name);
        }

        if (groupBy.Count == 0)
        {
            groupBy.AddRange(dimensions);
        }

        Guard.Ensure(!hasMeasures || dimensions.All(groupBy.Contains), ReportingRuleCodes.InvalidDefinition,
            "Every selected dimension must be part of the grouping when a measure is selected.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);

        var filters = new List<ReportFilter>();
        foreach (var filter in Filters ?? Array.Empty<ReportFilter>())
        {
            var field = ReportCatalog.Find(DataSource, filter.Field ?? string.Empty);
            Guard.Ensure(field is { Kind: FieldKind.Dimension }, ReportingRuleCodes.InvalidDefinition, $"Cannot filter on '{filter.Field}': filters apply to dimensions.",
                ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
            Guard.Ensure(Enum.IsDefined(filter.Operator) && !string.IsNullOrWhiteSpace(filter.Value) && filter.Value.Length <= 500, ReportingRuleCodes.InvalidDefinition,
                $"The filter on '{filter.Field}' is malformed.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
            if (field!.IsDate)
            {
                foreach (var value in filter.Value.Split(',', StringSplitOptions.TrimEntries))
                {
                    Guard.Ensure(IsIsoDate(value), ReportingRuleCodes.InvalidDefinition, $"The filter value '{value}' for '{filter.Field}' is not a date (yyyy-MM-dd or yyyy-MM).",
                        ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
                }
            }

            filters.Add(filter with { Field = field.Name });
        }

        return new ReportDefinition(DataSource, fields.Select(f => f.Name).ToArray(), filters, groupBy);
    }

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)
        || DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);
}
