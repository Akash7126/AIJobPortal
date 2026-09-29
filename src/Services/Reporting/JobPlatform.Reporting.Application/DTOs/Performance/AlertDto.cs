namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record AlertDto(Guid Id, Guid RuleId, string Metric, string Severity, decimal Value, decimal Threshold, DateTime RaisedAtUtc);
