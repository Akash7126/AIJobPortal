using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>The system metrics an alert rule may watch (handover 3.9, Q-04). Names are stable identifiers shared with the metrics source adapters.</summary>
public static class PerformanceMetrics
{
    public const string ResponseTimeP95Ms = "response_time_p95_ms";
    public const string CpuUtilisationPercent = "cpu_utilisation_percent";
    public const string MemoryUtilisationPercent = "memory_utilisation_percent";
    public const string ErrorRatePercent = "error_rate_percent";
    public const string RequestsPerSecond = "requests_per_second";

    public static readonly IReadOnlyList<string> Known = new[]
    {
        ResponseTimeP95Ms, CpuUtilisationPercent, MemoryUtilisationPercent, ErrorRatePercent, RequestsPerSecond
    };

    public static bool IsKnown(string? metric) => metric is not null && Known.Contains(metric);
}

public sealed record PerformanceAlertRaisedDomainEvent(DateTime At, Guid AlertId, string Metric, string Severity, decimal Value, decimal Threshold) : DomainEvent(At);

/// <summary>Threshold rule over a monitored metric (US-3.5.3-04). Fixed thresholds first; a statistical baseline is an open question (Q-06).</summary>
public sealed class PerformanceAlertRule : AggregateRoot<Guid>
{
    private PerformanceAlertRule()
    {
    }

    public string Metric { get; private set; } = string.Empty;
    public AlertComparator Comparator { get; private set; }
    public decimal Threshold { get; private set; }
    public int WindowMinutes { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static PerformanceAlertRule Create(string metric, AlertComparator comparator, decimal threshold, int windowMinutes, AlertSeverity severity, bool enabled, DateTime nowUtc)
    {
        var rule = new PerformanceAlertRule { Id = Guid.NewGuid() };
        rule.Configure(metric, comparator, threshold, windowMinutes, severity, enabled, nowUtc);
        return rule;
    }

    public void Configure(string metric, AlertComparator comparator, decimal threshold, int windowMinutes, AlertSeverity severity, bool enabled, DateTime nowUtc)
    {
        Guard.Ensure(PerformanceMetrics.IsKnown(metric), ReportingRuleCodes.InvalidAlertRule, $"Unknown metric '{metric}'.", ReportingErrorCodes.PerformanceInvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(Enum.IsDefined(comparator) && Enum.IsDefined(severity), ReportingRuleCodes.InvalidAlertRule, "Unknown comparator or severity.",
            ReportingErrorCodes.PerformanceInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(windowMinutes >= 1, ReportingRuleCodes.InvalidAlertRule, "The evaluation window must be at least one minute.", ReportingErrorCodes.PerformanceInvalidField,
            BusinessRuleKind.InvalidInput);
        Metric = metric;
        Comparator = comparator;
        Threshold = threshold;
        WindowMinutes = windowMinutes;
        Severity = severity;
        IsEnabled = enabled;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>AC-02: no alert while the value stays in the normal range; the alert fires when the threshold is crossed.</summary>
    public bool IsCrossedBy(decimal value) => Comparator switch
    {
        AlertComparator.GreaterThan => value > Threshold,
        AlertComparator.GreaterThanOrEqual => value >= Threshold,
        AlertComparator.LessThan => value < Threshold,
        _ => value <= Threshold
    };

    /// <summary>Evaluates one windowed sample: a disabled rule never fires. Returns the alert to persist, or null.</summary>
    public PerformanceAlert? Evaluate(decimal windowedValue, DateTime nowUtc) =>
        IsEnabled && IsCrossedBy(windowedValue) ? PerformanceAlert.Raise(this, windowedValue, nowUtc) : null;
}

/// <summary>A raised alert, retained 12 months and delivered in-app to administrators through BC-13 (US-3.5.3-04 AC-03).</summary>
public sealed class PerformanceAlert : AggregateRoot<Guid>
{
    public const int RetentionMonths = 12;

    private PerformanceAlert()
    {
    }

    public Guid RuleId { get; private set; }
    public string Metric { get; private set; } = string.Empty;
    public AlertSeverity Severity { get; private set; }
    public decimal Value { get; private set; }
    public decimal Threshold { get; private set; }
    public DateTime RaisedAtUtc { get; private set; }
    public DateTime RetainUntilUtc { get; private set; }

    public static PerformanceAlert Raise(PerformanceAlertRule rule, decimal value, DateTime nowUtc)
    {
        var alert = new PerformanceAlert
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Metric = rule.Metric,
            Severity = rule.Severity,
            Value = value,
            Threshold = rule.Threshold,
            RaisedAtUtc = nowUtc,
            RetainUntilUtc = nowUtc.AddMonths(RetentionMonths)
        };
        alert.Raise(new PerformanceAlertRaisedDomainEvent(nowUtc, alert.Id, alert.Metric, alert.Severity.ToString(), value, rule.Threshold));
        return alert;
    }
}
