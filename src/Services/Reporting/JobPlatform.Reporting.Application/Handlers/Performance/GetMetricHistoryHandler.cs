using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class GetMetricHistoryHandler : IQueryHandler<GetMetricHistoryQuery, MetricHistoryDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly TimeProvider _clock;
    private readonly PerformanceService _performanceService;

    public GetMetricHistoryHandler(IAnalyticsQueryService analytics, TimeProvider clock, PerformanceService performanceService)
    {
        _analytics = analytics;
        _clock = clock;
        _performanceService = performanceService;
    }

    public async Task<Result<MetricHistoryDto>> Handle(GetMetricHistoryQuery request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(GetMetricHistoryQuery), ct) is { } denied)
        {
            return denied;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var granularity = (request.Granularity ?? "day").ToLowerInvariant();
        var rows = await _analytics.MetricsAsync(request.Metric, DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), 200_000, ct);
        var points = rows.GroupBy(r => Periods.Key(r.SampledAtUtc, granularity)).OrderBy(g => g.Key)
            .Select(g => new HistoryPointDto(g.Key, Math.Round(g.Average(r => r.Value), 4), g.Max(r => r.Value), g.Count())).ToList();
        return new MetricHistoryDto(request.Metric, range.From, range.To, granularity, points);
    }
}
