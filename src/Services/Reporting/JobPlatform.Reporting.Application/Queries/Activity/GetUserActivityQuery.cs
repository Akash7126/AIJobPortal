using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Activity;

// Module A - user activity monitoring (US-3.5.1-01..03) plus the scheduled maintenance commands (retention, rollup rebuild).

public sealed record GetUserActivityQuery(DateOnly? From, DateOnly? To, string? Type) : ActivityRequest, IQuery<UserActivityDto>;
