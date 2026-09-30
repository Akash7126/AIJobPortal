using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Employment;

public sealed record GetEmploymentOutcomesQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<EmploymentOutcomesDto>;
