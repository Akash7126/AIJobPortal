using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Performance;

public sealed record GetPerformanceDashboardQuery(DateOnly? From, DateOnly? To) : PerformanceRequest, IQuery<PerformanceDashboardDto>;
