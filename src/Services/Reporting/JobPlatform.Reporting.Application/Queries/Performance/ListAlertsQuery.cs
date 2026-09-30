using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.Performance;

public sealed record ListAlertsQuery(int Take) : PerformanceRequest, IQuery<IReadOnlyList<AlertDto>>;
