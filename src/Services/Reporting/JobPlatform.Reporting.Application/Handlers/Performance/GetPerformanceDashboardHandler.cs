using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class GetPerformanceDashboardHandler : IQueryHandler<GetPerformanceDashboardQuery, PerformanceDashboardDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly IPerformanceAlertRuleRepository _rules;
    private readonly TimeProvider _clock;
    private readonly PerformanceService _performanceService;

    public GetPerformanceDashboardHandler(IAnalyticsQueryService analytics, IPerformanceAlertRuleRepository rules, TimeProvider clock, PerformanceService performanceService)
    {
        _analytics = analytics;
        _rules = rules;
        _clock = clock;
        _performanceService = performanceService;
    }

    public async Task<Result<PerformanceDashboardDto>> Handle(GetPerformanceDashboardQuery request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(GetPerformanceDashboardQuery), ct) is { } denied)
        {
            return denied;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var alerts = (await _rules.ListAlertsAsync(5, ct)).Select(PerformanceService.ToDto).ToList();
        return new PerformanceDashboardDto(await new PerformanceReader(_analytics).ReadAsync(range, ct), await _performanceService.UsageAsync(range, ct), alerts);
    }
}
