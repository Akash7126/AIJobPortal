using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Performance;

public sealed record GetUsagePatternsQuery(DateOnly? From, DateOnly? To) : PerformanceRequest, IQuery<UsagePatternsDto>;
