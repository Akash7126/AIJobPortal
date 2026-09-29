using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.Performance;

public sealed record ListAlertRulesQuery : PerformanceRequest, IQuery<IReadOnlyList<AlertRuleDto>>;
