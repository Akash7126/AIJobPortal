using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetEmploymentMetricsHandler : IQueryHandler<GetEmploymentMetricsQuery, EmploymentMetricsDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetEmploymentMetricsHandler(IAnalyticsQueryService analytics, StatisticsPolicy policy, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _policy = policy;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<EmploymentMetricsDto>> Handle(GetEmploymentMetricsQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetEmploymentMetricsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var granularity = (request.Granularity ?? "day").ToLowerInvariant();
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        var trend = postings.GroupBy(p => Periods.Key(p.FirstSeenAtUtc, granularity)).OrderBy(g => g.Key).Select(g => new TrendPointDto(g.Key, g.Count())).ToList();

        // Application and hire events do not exist (handover Q-01 / gap G-01): the rates are reported as unavailable, never as zero.
        const string noSource = "NoApplicationOrHireEvents";
        var closedPostings = postings.Where(p => p.ClosedAtUtc is not null).ToList();
        var timeToClose = StatisticsCalculator.Measure(_policy, closedPostings.Count,
            () => Math.Round((decimal)closedPostings.Average(p => (p.ClosedAtUtc!.Value - p.FirstSeenAtUtc).TotalDays), 2));
        return new EmploymentMetricsDto(range.From, range.To, granularity, _policy.IsInsufficient(postings.Count), trend,
            new FigureDto(true, null, 0, noSource), new FigureDto(true, null, 0, noSource),
            new FigureDto(timeToClose.InsufficientData, timeToClose.Value, timeToClose.SampleSize, timeToClose.InsufficientData ? "InsufficientData" : "TimeFromPostingToClosure"));
    }
}
