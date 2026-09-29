using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetSkillDemandTrendsHandler : IQueryHandler<GetSkillDemandTrendsQuery, SkillTrendsDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetSkillDemandTrendsHandler(IAnalyticsQueryService analytics, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<SkillTrendsDto>> Handle(GetSkillDemandTrendsQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetSkillDemandTrendsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var granularity = (request.Granularity ?? "month").ToLowerInvariant();
        var skills = await _analytics.SkillsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        var demand = skills.Where(s => s.Side == FactSkillDemand.Demand).ToList();
        var periods = demand.Select(s => Periods.Key(s.OccurredAtUtc, granularity)).Distinct().OrderBy(p => p).ToList();
        if (periods.Count < 2)
        {
            return new SkillTrendsDto(range.From, range.To, granularity, true, Array.Empty<SkillTrendItemDto>());
        }

        var latest = periods[^1];
        var previous = periods[^2];
        var supply = skills.Where(s => s.Side == FactSkillDemand.Supply).GroupBy(s => s.Skill).ToDictionary(g => g.Key, g => (long)g.Count());
        var items = demand.GroupBy(s => s.Skill).Select(g =>
        {
            var now = g.LongCount(s => Periods.Key(s.OccurredAtUtc, granularity) == latest);
            var before = g.LongCount(s => Periods.Key(s.OccurredAtUtc, granularity) == previous);
            var growth = StatisticsCalculator.Growth(before, now);
            var totalSupply = supply.GetValueOrDefault(g.Key);
            return new SkillTrendItemDto(g.Key, now, before, growth, totalSupply, g.LongCount() - totalSupply, before == 0 && now > 0 || growth is > 0.25m);
        }).OrderByDescending(i => i.Demand).ThenBy(i => i.Skill).Take(100).ToList();
        return new SkillTrendsDto(range.From, range.To, granularity, false, items);
    }
}
