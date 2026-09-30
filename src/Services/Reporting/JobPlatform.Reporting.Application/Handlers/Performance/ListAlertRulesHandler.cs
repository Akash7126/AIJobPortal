using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class ListAlertRulesHandler : IQueryHandler<ListAlertRulesQuery, IReadOnlyList<AlertRuleDto>>
{
    private readonly IPerformanceAlertRuleRepository _rules;
    private readonly PerformanceService _performanceService;

    public ListAlertRulesHandler(IPerformanceAlertRuleRepository rules, PerformanceService performanceService)
    {
        _rules = rules;
        _performanceService = performanceService;
    }

    public async Task<Result<IReadOnlyList<AlertRuleDto>>> Handle(ListAlertRulesQuery request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(ListAlertRulesQuery), ct) is { } denied)
        {
            return denied;
        }

        return (await _rules.ListAsync(ct)).Select(PerformanceService.ToDto).ToList();
    }
}
