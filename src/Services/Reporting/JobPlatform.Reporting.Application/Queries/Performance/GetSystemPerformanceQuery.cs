using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Performance;

// Module C - system performance (US-3.5.3-01..05). Technical metrics come from the telemetry source (Q-04); matching figures from FactMatch.

public sealed record GetSystemPerformanceQuery(DateOnly? From, DateOnly? To) : PerformanceRequest, IQuery<SystemPerformanceDto>;
