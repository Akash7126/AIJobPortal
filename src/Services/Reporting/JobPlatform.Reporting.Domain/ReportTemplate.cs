using System.Globalization;
using System.Text.Json;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>One declared parameter of a template (handover 3.2). Numeric and date bounds are compared as decimals / ISO dates.</summary>
public sealed record TemplateParameter(string Name, ParameterType Type, string? Min = null, string? Max = null, string? Default = null, IReadOnlyList<string>? Options = null)
{
    /// <summary>INV-03: the parameter is well formed (type, range, default in range, enum options).</summary>
    public void Validate()
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(Name) && Name.Length <= 64, ReportingRuleCodes.InvalidTemplateParameter, "A parameter needs a name of up to 64 characters.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(Enum.IsDefined(Type), ReportingRuleCodes.InvalidTemplateParameter, $"Parameter '{Name}' has an unknown type.", ReportingErrorCodes.CustomInvalidField,
            BusinessRuleKind.InvalidInput);

        if (Type == ParameterType.Enum)
        {
            Guard.Ensure(Options is { Count: > 0 }, ReportingRuleCodes.InvalidTemplateParameter, $"Enum parameter '{Name}' needs options.", ReportingErrorCodes.CustomInvalidField,
                BusinessRuleKind.InvalidInput);
            Guard.Ensure(Default is null || Options!.Contains(Default), ReportingRuleCodes.InvalidTemplateParameter, $"Default of '{Name}' is not one of its options.",
                ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
            return;
        }

        if (Type is ParameterType.Int or ParameterType.Decimal or ParameterType.Date)
        {
            var lo = TryNumber(Min);
            var hi = TryNumber(Max);
            var def = TryNumber(Default);
            Guard.Ensure((Min is null || lo is not null) && (Max is null || hi is not null) && (Default is null || def is not null),
                ReportingRuleCodes.InvalidTemplateParameter, $"Parameter '{Name}' has a bound or default that is not a valid {Type}.", ReportingErrorCodes.CustomInvalidField,
                BusinessRuleKind.InvalidInput);
            Guard.Ensure(lo is null || hi is null || lo <= hi, ReportingRuleCodes.InvalidTemplateParameter, $"Parameter '{Name}' has min greater than max.",
                ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
            Guard.Ensure(def is null || ((lo is null || def >= lo) && (hi is null || def <= hi)), ReportingRuleCodes.InvalidTemplateParameter,
                $"Default of '{Name}' is outside its range.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        }
    }

    /// <summary>True when the supplied value satisfies this parameter (used when a template is run).</summary>
    public bool Accepts(string? value)
    {
        if (value is null)
        {
            return true;
        }

        switch (Type)
        {
            case ParameterType.String:
                return true;
            case ParameterType.Enum:
                return Options is not null && Options.Contains(value);
            default:
                var number = TryNumber(value);
                if (number is null)
                {
                    return false;
                }

                var lo = TryNumber(Min);
                var hi = TryNumber(Max);
                return (lo is null || number >= lo) && (hi is null || number <= hi);
        }
    }

    private decimal? TryNumber(string? text)
    {
        if (text is null)
        {
            return null;
        }

        if (Type == ParameterType.Date)
        {
            return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.DayNumber : null;
        }

        if (Type == ParameterType.Int)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}

/// <summary>Reusable report definition (US-3.5.4-02). A later save always wins (AC-04): every save increments <see cref="Revision"/>.</summary>
public sealed class ReportTemplate : AggregateRoot<Guid>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ReportTemplate()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public ReportDataSource DataSource { get; private set; }
    public string ParametersJson { get; private set; } = "[]";
    public int Revision { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<TemplateParameter> Parameters => JsonSerializer.Deserialize<List<TemplateParameter>>(ParametersJson, Json) ?? new List<TemplateParameter>();

    public static ReportTemplate Create(string name, ReportDataSource dataSource, IReadOnlyList<TemplateParameter> parameters, Guid administratorId, DateTime nowUtc)
    {
        var template = new ReportTemplate { Id = Guid.NewGuid(), CreatedBy = administratorId, CreatedAtUtc = nowUtc };
        template.Save(name, dataSource, parameters, nowUtc);
        return template;
    }

    public void Save(string name, ReportDataSource dataSource, IReadOnlyList<TemplateParameter> parameters, DateTime nowUtc)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(name) && name.Length <= 150, ReportingRuleCodes.InvalidTemplateParameter, "A template needs a name of up to 150 characters.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(Enum.IsDefined(dataSource), ReportingRuleCodes.InvalidTemplateParameter, "Unknown data source.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(parameters.Count <= 30, ReportingRuleCodes.InvalidTemplateParameter, "A template may declare at most 30 parameters.", ReportingErrorCodes.CustomInvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(parameters.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == parameters.Count, ReportingRuleCodes.InvalidTemplateParameter,
            "Parameter names must be unique.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        foreach (var parameter in parameters)
        {
            parameter.Validate();
        }

        Name = name.Trim();
        DataSource = dataSource;
        ParametersJson = JsonSerializer.Serialize(parameters, Json);
        Revision++;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Resolves the run-time arguments: supplied values are checked against the declaration, missing ones take their default.</summary>
    public IReadOnlyDictionary<string, string> ResolveArguments(IReadOnlyDictionary<string, string>? supplied)
    {
        supplied ??= new Dictionary<string, string>();
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declared = Parameters;
        foreach (var name in supplied.Keys)
        {
            Guard.Ensure(declared.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), ReportingRuleCodes.InvalidTemplateParameter,
                $"Parameter '{name}' is not declared by the template.", ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        }

        foreach (var parameter in declared)
        {
            supplied.TryGetValue(parameter.Name, out var value);
            value ??= parameter.Default;
            Guard.Ensure(parameter.Accepts(value), ReportingRuleCodes.InvalidTemplateParameter, $"Value of '{parameter.Name}' is outside its declared type or range.",
                ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
            if (value is not null)
            {
                resolved[parameter.Name] = value;
            }
        }

        return resolved;
    }
}
