namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record AlertRuleDto(Guid Id, string Metric, string Comparator, decimal Threshold, int WindowMinutes, string Severity, bool Enabled);
