using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Employment;

/// <param name="By">industry, position or location.</param>
public sealed record GetSalaryAnalyticsQuery(DateOnly? From, DateOnly? To, string? By) : EmploymentRequest, IQuery<SalaryAnalyticsDto>;
