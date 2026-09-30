using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetIndustryAnalyticsHandler : IQueryHandler<GetIndustryAnalyticsQuery, IndustryAnalyticsDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetIndustryAnalyticsHandler(IAnalyticsQueryService analytics, StatisticsPolicy policy, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _policy = policy;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<IndustryAnalyticsDto>> Handle(GetIndustryAnalyticsQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetIndustryAnalyticsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct);
        var rows = postings.GroupBy(p => StatisticsCalculator.Bucket(p.Category)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => _policy.IsSuppressed(g.Count())
                ? new IndustryRowDto(g.Key, null, null, true)
                : new IndustryRowDto(g.Key, g.Count(), g.Count(p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase)), false)).ToList();
        // Supply per industry needs an industry dimension on candidates (handover Q-01): only the demand side is available.
        return new IndustryAnalyticsDto(range.From, range.To, _policy.IsInsufficient(postings.Count), false, rows);
    }
}
