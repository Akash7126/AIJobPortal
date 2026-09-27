using System.Globalization;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.Settings;

public enum SettingValueType
{
    Int,
    Decimal,
    Bool,
    String,
    Enum
}

/// <summary>Bounds of a setting, per value type (GAP-002): numeric min/max, an allowed set for enums, a maximum length for strings.</summary>
public sealed class SettingBounds : ValueObject
{
    public static readonly SettingBounds None = new(null, null, Array.Empty<string>(), null);

    public SettingBounds(decimal? min, decimal? max, IReadOnlyList<string> allowedValues, int? maxLength)
    {
        Min = min;
        Max = max;
        AllowedValues = allowedValues;
        MaxLength = maxLength;
    }

    public decimal? Min { get; }

    public decimal? Max { get; }

    public IReadOnlyList<string> AllowedValues { get; }

    public int? MaxLength { get; }

    public static SettingBounds Range(decimal min, decimal max) => new(min, max, Array.Empty<string>(), null);

    public static SettingBounds OneOf(params string[] values) => new(null, null, values, null);

    public static SettingBounds Length(int maxLength) => new(null, null, Array.Empty<string>(), maxLength);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
        yield return MaxLength;
        foreach (var value in AllowedValues)
        {
            yield return value;
        }
    }
}

/// <summary>Append-only history row of a setting (US-3.1.4-06 AC-03: later save wins and the change is logged).</summary>
public sealed class SystemSettingChange : Entity<Guid>
{
    private SystemSettingChange()
    {
        OldValue = string.Empty;
        NewValue = string.Empty;
    }

    public Guid SettingId { get; private set; }

    public string OldValue { get; private set; }

    public string NewValue { get; private set; }

    public Guid ChangedBy { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public int SettingVersion { get; private set; }

    internal static SystemSettingChange Record(Guid settingId, string oldValue, string newValue, Guid changedBy, DateTime at, int version) =>
        new() { Id = Guid.NewGuid(), SettingId = settingId, OldValue = oldValue, NewValue = newValue, ChangedBy = changedBy, ChangedAtUtc = at, SettingVersion = version };
}

/// <summary>A definition of a platform setting (the initial catalogue is fixed by configuration of the platform, Q-03).</summary>
public sealed record SettingDefinition(string Key, SettingValueType ValueType, string DefaultValue, SettingBounds Bounds, string Description);

/// <summary>The initial list of settings (Q-03 asks for it to be agreed; this is the conservative starting set). Settings owned by other BCs are not duplicated here.</summary>
public static class SystemSettingCatalog
{
    public static readonly IReadOnlyList<SettingDefinition> Definitions = new[]
    {
        new SettingDefinition("upload.maxSizeMb", SettingValueType.Int, "5", SettingBounds.Range(1, 50), "Maximum size of an uploaded file, in megabytes."),
        new SettingDefinition("retention.months", SettingValueType.Int, "12", SettingBounds.Range(1, 120), "Months data such as logs is kept live before archiving."),
        new SettingDefinition("posting.defaultDeadlineDays", SettingValueType.Int, "30", SettingBounds.Range(1, 365), "Default number of days a posting stays open."),
        new SettingDefinition("platform.maintenanceMode", SettingValueType.Bool, "false", SettingBounds.None, "Puts the platform into maintenance mode."),
        new SettingDefinition("platform.defaultLanguage", SettingValueType.Enum, "ar", SettingBounds.OneOf("ar", "en"), "Default interface language."),
        new SettingDefinition("platform.supportEmail", SettingValueType.String, "support@jobplatform.local", SettingBounds.Length(254), "Support contact e-mail address.")
    };

    public static SettingDefinition? Find(string key) =>
        Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));
}

/// <summary>
/// Typed, bounded, named platform parameter (proposed aggregate, story US-3.1.4-06). A change takes effect on save; concurrent changes follow
/// "later save wins" and every change appends a history row.
/// </summary>
public sealed class SystemSetting : AggregateRoot<Guid>
{
    private readonly List<SystemSettingChange> _history = new();

    private SystemSetting()
    {
        Key = string.Empty;
        Value = string.Empty;
        Bounds = SettingBounds.None;
    }

    public string Key { get; private set; }

    public SettingValueType ValueType { get; private set; }

    public string Value { get; private set; }

    public SettingBounds Bounds { get; private set; }

    /// <summary>Incremented on every saved change.</summary>
    public int SettingVersion { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>History rows appended in this unit of work (older rows are not loaded with the aggregate).</summary>
    public IReadOnlyCollection<SystemSettingChange> History => _history;

    /// <summary>Creates a setting from its definition (seeding). Raises no event: nothing changed for consumers.</summary>
    public static SystemSetting Define(SettingDefinition definition, DateTime nowUtc)
    {
        var value = Normalise(definition.ValueType, definition.DefaultValue, definition.Bounds);
        return new SystemSetting
        {
            Id = Guid.NewGuid(),
            Key = definition.Key,
            ValueType = definition.ValueType,
            Value = value,
            Bounds = definition.Bounds,
            SettingVersion = 1,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>INV-08: the value must parse to the value type and lie within the bounds (E-AUM-INVALID-FIELD). Administrator only.</summary>
    public void Change(string? rawValue, Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor));
        var newValue = Normalise(ValueType, rawValue, Bounds);
        var previous = Value;
        Value = newValue;
        SettingVersion++;
        UpdatedBy = actor.Id;
        UpdatedAtUtc = nowUtc;
        _history.Add(SystemSettingChange.Record(Id, previous, newValue, actor.Id, nowUtc, SettingVersion));
        Raise(new SystemSettingChangedDomainEvent(Id, Key, SettingVersion, actor.Id, nowUtc));
    }

    /// <summary>Parses to the canonical text of the type and checks the bounds. Throws the INV-08 violation otherwise.</summary>
    public static string Normalise(SettingValueType type, string? raw, SettingBounds bounds)
    {
        const string field = "value";
        var text = raw?.Trim() ?? string.Empty;
        switch (type)
        {
            case SettingValueType.Int:
            {
                var parsed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
                Rules.EnsureValid(!parsed, RuleCodes.SettingInvalidValue, "The value must be a whole number.", field);
                CheckRange(number, bounds);
                return number.ToString(CultureInfo.InvariantCulture);
            }
            case SettingValueType.Decimal:
            {
                var parsed = decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number);
                Rules.EnsureValid(!parsed, RuleCodes.SettingInvalidValue, "The value must be a number.", field);
                CheckRange(number, bounds);
                return number.ToString(CultureInfo.InvariantCulture);
            }
            case SettingValueType.Bool:
            {
                var parsed = bool.TryParse(text, out var flag);
                Rules.EnsureValid(!parsed, RuleCodes.SettingInvalidValue, "The value must be true or false.", field);
                return flag ? "true" : "false";
            }
            case SettingValueType.Enum:
            {
                var match = bounds.AllowedValues.FirstOrDefault(v => string.Equals(v, text, StringComparison.OrdinalIgnoreCase));
                Rules.EnsureValid(match is null, RuleCodes.SettingOutOfRange, $"The value must be one of: {string.Join(", ", bounds.AllowedValues)}.", field);
                return match!;
            }
            default:
                Rules.EnsureValid(text.Length == 0, RuleCodes.SettingInvalidValue, "The value must not be empty.", field);
                Rules.EnsureValid(bounds.MaxLength is { } max && text.Length > max, RuleCodes.SettingOutOfRange,
                    $"The value must not exceed {bounds.MaxLength} characters.", field);
                return text;
        }
    }

    private static void CheckRange(decimal number, SettingBounds bounds)
    {
        Rules.EnsureValid(bounds.Min is { } min && number < min || bounds.Max is { } max && number > max, RuleCodes.SettingOutOfRange,
            $"The value must be between {bounds.Min} and {bounds.Max}.", "value");
    }
}

/// <summary>Cheap, state-free check used by the application validators: does the text parse to the value type? (Bounds are a domain rule, INV-08.)</summary>
public static class SettingValueParser
{
    public static bool CanParse(SettingValueType type, string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;
        return type switch
        {
            SettingValueType.Int => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            SettingValueType.Decimal => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            SettingValueType.Bool => bool.TryParse(text, out _),
            _ => text.Length > 0
        };
    }
}
