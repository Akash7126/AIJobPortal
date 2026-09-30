using FluentValidation;
using JobPlatform.Reporting.Application.DTOs.Employment;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Services.Employment;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Employment;

internal sealed class GetSalaryAnalyticsHandler : IQueryHandler<GetSalaryAnalyticsQuery, SalaryAnalyticsDto>
{
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly EmploymentService _employmentService;

    public GetSalaryAnalyticsHandler(IAnalyticsQueryService analytics, StatisticsPolicy policy, TimeProvider clock, EmploymentService employmentService)
    {
        _analytics = analytics;
        _policy = policy;
        _clock = clock;
        _employmentService = employmentService;
    }

    public async Task<Result<SalaryAnalyticsDto>> Handle(GetSalaryAnalyticsQuery request, CancellationToken ct)
    {
        if ((await _employmentService.Guard(nameof(GetSalaryAnalyticsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var by = (request.By ?? "industry").ToLowerInvariant();
        var withSalary = (await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), EmploymentService.Max, ct))
            .Where(p => p.SalaryMin is not null || p.SalaryMax is not null).ToList();
        var rows = withSalary.GroupBy(p => StatisticsCalculator.Bucket(by switch { "position" => p.Title, "location" => p.Location, _ => p.Category }))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g =>
            {
                if (_policy.IsSuppressed(g.Count()))
                {
                    return new SalaryRowDto(g.Key, null, null, null, null, true);
                }

                var mids = g.Select(p => StatisticsCalculator.Midpoint(p.SalaryMin, p.SalaryMax)).ToList();
                return new SalaryRowDto(g.Key, g.Count(), g.Min(p => p.SalaryMin ?? p.SalaryMax), Math.Round(mids.Average(), 2), g.Max(p => p.SalaryMax ?? p.SalaryMin), false);
            }).ToList();
        return new SalaryAnalyticsDto(range.From, range.To, by, _policy.IsInsufficient(withSalary.Count), rows);
    }
}
