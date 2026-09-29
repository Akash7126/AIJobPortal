using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Employment;

// Module B - employment statistics (US-3.5.2-01..07). Policies (insufficient data, small cells, "unspecified" bucket, exclusion of records without follow-up)
// are applied here, on raw rows read from the analytics store, so they are unit-testable without a database.

public sealed record GetEmploymentStatisticsQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<EmploymentStatisticsDto>;
