using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Performance;

/// <summary>Creates a rule (Id null) or replaces an existing one (later save wins).</summary>
public sealed record ConfigurePerformanceAlertRuleCommand(Guid? Id, string Metric, string Comparator, decimal Threshold, int WindowMinutes, string Severity, bool Enabled)
    : PerformanceCommandRequest, ICommand<AlertRuleDto>;
