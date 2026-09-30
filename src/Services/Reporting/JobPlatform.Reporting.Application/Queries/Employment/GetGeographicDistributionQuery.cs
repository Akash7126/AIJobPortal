using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Employment;

public sealed record GetGeographicDistributionQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<GeographyDto>;
