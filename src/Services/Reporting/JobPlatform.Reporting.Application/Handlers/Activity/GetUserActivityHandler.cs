using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Queries.Activity;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Activity;

internal sealed class GetUserActivityHandler : IQueryHandler<GetUserActivityQuery, UserActivityDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly IAnalyticsQueryService _analytics;
    private readonly TimeProvider _clock;

    public GetUserActivityHandler(IReportAccessGuard guard, IAnalyticsQueryService analytics, TimeProvider clock)
    {
        _guard = guard;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<Result<UserActivityDto>> Handle(GetUserActivityQuery request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.ActivityLogs, nameof(GetUserActivityQuery), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var counts = await _analytics.DailyCountsAsync("activity.", range.From, range.To, ct);
        var byType = counts.GroupBy(c => c.Metric["activity.".Length..]).ToDictionary(g => g.Key, g => g.Sum(c => c.Count), StringComparer.OrdinalIgnoreCase);
        var types = request.Type is null ? ActivityTypes.All : ActivityTypes.All.Where(t => t.Equals(request.Type, StringComparison.OrdinalIgnoreCase)).ToArray();
        var items = types.Select(t => new ActivityCountDto(t, byType.GetValueOrDefault(t))).ToList();
        return new UserActivityDto(range.From, range.To, items.Sum(i => i.Count), items);
    }
}
