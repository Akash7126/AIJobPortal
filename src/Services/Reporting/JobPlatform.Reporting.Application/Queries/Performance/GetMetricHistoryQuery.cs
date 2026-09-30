using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Performance;

public sealed record GetMetricHistoryQuery(string Metric, DateOnly? From, DateOnly? To, string? Granularity) : PerformanceRequest, IQuery<MetricHistoryDto>;
