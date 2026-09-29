using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Employment;

public sealed record GetEmploymentMetricsQuery(DateOnly? From, DateOnly? To, string? Granularity) : EmploymentRequest, IQuery<EmploymentMetricsDto>;
