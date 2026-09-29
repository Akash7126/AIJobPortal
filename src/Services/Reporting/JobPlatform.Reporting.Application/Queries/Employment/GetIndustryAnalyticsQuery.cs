using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Employment;

public sealed record GetIndustryAnalyticsQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<IndustryAnalyticsDto>;
