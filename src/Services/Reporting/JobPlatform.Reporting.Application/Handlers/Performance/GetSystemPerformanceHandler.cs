using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class GetSystemPerformanceHandler : IQueryHandler<GetSystemPerformanceQuery, SystemPerformanceDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly TimeProvider _clock;
    private readonly PerformanceService _performanceService;

    public GetSystemPerformanceHandler(IAnalyticsQueryService analytics, TimeProvider clock, PerformanceService performanceService)
    {
        _analytics = analytics;
        _clock = clock;
        _performanceService = performanceService;
    }

    public async Task<Result<SystemPerformanceDto>> Handle(GetSystemPerformanceQuery request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(GetSystemPerformanceQuery), ct) is { } denied)
        {
            return denied;
        }

        return await new PerformanceReader(_analytics).ReadAsync(DateRanges.Resolve(request.From, request.To, _clock), ct);
    }
}
