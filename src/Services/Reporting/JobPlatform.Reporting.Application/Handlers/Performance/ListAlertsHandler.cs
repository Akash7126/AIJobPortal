using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class ListAlertsHandler : IQueryHandler<ListAlertsQuery, IReadOnlyList<AlertDto>>
{
    private readonly IPerformanceAlertRuleRepository _rules;
    private readonly PerformanceService _performanceService;

    public ListAlertsHandler(IPerformanceAlertRuleRepository rules, PerformanceService performanceService)
    {
        _rules = rules;
        _performanceService = performanceService;
    }

    public async Task<Result<IReadOnlyList<AlertDto>>> Handle(ListAlertsQuery request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(ListAlertsQuery), ct) is { } denied)
        {
            return denied;
        }

        return (await _rules.ListAlertsAsync(request.Take, ct)).Select(PerformanceService.ToDto).ToList();
    }
}
