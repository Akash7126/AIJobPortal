using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class GetUsagePatternsHandler : IQueryHandler<GetUsagePatternsQuery, UsagePatternsDto>
{
    private readonly TimeProvider _clock;
    private readonly PerformanceService _performanceService;

    public GetUsagePatternsHandler(TimeProvider clock, PerformanceService performanceService)
    {
        _clock = clock;
        _performanceService = performanceService;
    }

    public async Task<Result<UsagePatternsDto>> Handle(GetUsagePatternsQuery request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(GetUsagePatternsQuery), ct) is { } denied)
        {
            return denied;
        }

        return await _performanceService.UsageAsync(DateRanges.Resolve(request.From, request.To, _clock), ct);
    }
}
