using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application;

// Module B - employment statistics (US-3.5.2-01..07). Policies (insufficient data, small cells, "unspecified" bucket, exclusion of records without follow-up)
// are applied here, on raw rows read from the analytics store, so they are unit-testable without a database.

public sealed record GetEmploymentStatisticsQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<EmploymentStatisticsDto>;

public sealed record GetEmploymentMetricsQuery(DateOnly? From, DateOnly? To, string? Granularity) : EmploymentRequest, IQuery<EmploymentMetricsDto>;

public sealed record GetIndustryAnalyticsQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<IndustryAnalyticsDto>;

public sealed record GetSkillDemandTrendsQuery(DateOnly? From, DateOnly? To, string? Granularity) : EmploymentRequest, IQuery<SkillTrendsDto>;

public sealed record GetGeographicDistributionQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<GeographyDto>;

/// <param name="By">industry, position or location.</param>
public sealed record GetSalaryAnalyticsQuery(DateOnly? From, DateOnly? To, string? By) : EmploymentRequest, IQuery<SalaryAnalyticsDto>;

public sealed record GetEmploymentOutcomesQuery(DateOnly? From, DateOnly? To) : EmploymentRequest, IQuery<EmploymentOutcomesDto>;

public sealed class GetEmploymentStatisticsValidator : AbstractValidator<GetEmploymentStatisticsQuery>
{
    public GetEmploymentStatisticsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

public sealed class GetEmploymentMetricsValidator : AbstractValidator<GetEmploymentMetricsQuery>
{
    public GetEmploymentMetricsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To, x => x.Granularity);
}

public sealed class GetIndustryAnalyticsValidator : AbstractValidator<GetIndustryAnalyticsQuery>
{
    public GetIndustryAnalyticsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

public sealed class GetSkillDemandTrendsValidator : AbstractValidator<GetSkillDemandTrendsQuery>
{
    public GetSkillDemandTrendsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To, x => x.Granularity);
}

public sealed class GetGeographicDistributionValidator : AbstractValidator<GetGeographicDistributionQuery>
{
    public GetGeographicDistributionValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

public sealed class GetSalaryAnalyticsValidator : AbstractValidator<GetSalaryAnalyticsQuery>
{
    public static readonly string[] Groupings = { "industry", "position", "location" };

    public GetSalaryAnalyticsValidator()
    {
        DateRangeRules.AddTo(this, x => x.From, x => x.To);
        RuleFor(x => x.By).Must(b => b is null || Groupings.Contains(b, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.By.Unknown");
    }
}

public sealed class GetEmploymentOutcomesValidator : AbstractValidator<GetEmploymentOutcomesQuery>
{
    public GetEmploymentOutcomesValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}

/// <summary>Period keys for trends: day yyyy-MM-dd, week (Monday) yyyy-MM-dd, month yyyy-MM.</summary>
public static class Periods
{
    public static string Key(DateTime utc, string granularity) => granularity.ToLowerInvariant() switch
    {
        "month" => utc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
        "week" => utc.Date.AddDays(-(((int)utc.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        _ => utc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
    };
}

internal sealed class EmploymentHandlers :
    IQueryHandler<GetEmploymentStatisticsQuery, EmploymentStatisticsDto>,
    IQueryHandler<GetEmploymentMetricsQuery, EmploymentMetricsDto>,
    IQueryHandler<GetIndustryAnalyticsQuery, IndustryAnalyticsDto>,
    IQueryHandler<GetSkillDemandTrendsQuery, SkillTrendsDto>,
    IQueryHandler<GetGeographicDistributionQuery, GeographyDto>,
    IQueryHandler<GetSalaryAnalyticsQuery, SalaryAnalyticsDto>,
    IQueryHandler<GetEmploymentOutcomesQuery, EmploymentOutcomesDto>
{
    private const int Max = 200_000;

    private readonly IReportAccessGuard _guard;
    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;
    private readonly TimeProvider _clock;

    public EmploymentHandlers(IReportAccessGuard guard, IAnalyticsQueryService analytics, StatisticsPolicy policy, TimeProvider clock)
    {
        _guard = guard;
        _analytics = analytics;
        _policy = policy;
        _clock = clock;
    }

    private Task<Result<Unit2>> Guard(string name, CancellationToken ct) => GuardAsync(name, ct);

    private async Task<Result<Unit2>> GuardAsync(string name, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.EmploymentStatistics, name, ct);
        return access.IsFailure ? access.Error! : Unit2.Value;
    }

    public async Task<Result<EmploymentStatisticsDto>> Handle(GetEmploymentStatisticsQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetEmploymentStatisticsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
        var registrations = await _analytics.RegistrationsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
        var sample = postings.Count + registrations.Count;
        if (_policy.IsInsufficient(sample))
        {
            return new EmploymentStatisticsDto(range.From, range.To, true, sample, null, null);
        }

        var active = postings.Count(p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase));
        var closed = postings.Count(p => FactJobPosting.IsTerminal(p.Status));
        return new EmploymentStatisticsDto(range.From, range.To, false, sample,
            new PostingCountsDto(postings.Count, active, closed, postings.Count(p => p.Source == "External")),
            new RegistrationCountsDto(registrations.Count(r => r.Milestone == "AccountCreated" && r.ActorType == "JobSeeker"),
                registrations.Count(r => r.Milestone == "AccountCreated" && r.ActorType == "Employer"),
                registrations.Count(r => r.Milestone == "ProfileCreated"), registrations.Count(r => r.Milestone == "AccountApproved")));
    }

    public async Task<Result<EmploymentMetricsDto>> Handle(GetEmploymentMetricsQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetEmploymentMetricsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var granularity = (request.Granularity ?? "day").ToLowerInvariant();
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
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

    public async Task<Result<IndustryAnalyticsDto>> Handle(GetIndustryAnalyticsQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetIndustryAnalyticsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
        var rows = postings.GroupBy(p => StatisticsCalculator.Bucket(p.Category)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => _policy.IsSuppressed(g.Count())
                ? new IndustryRowDto(g.Key, null, null, true)
                : new IndustryRowDto(g.Key, g.Count(), g.Count(p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase)), false)).ToList();
        // Supply per industry needs an industry dimension on candidates (handover Q-01): only the demand side is available.
        return new IndustryAnalyticsDto(range.From, range.To, _policy.IsInsufficient(postings.Count), false, rows);
    }

    public async Task<Result<SkillTrendsDto>> Handle(GetSkillDemandTrendsQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetSkillDemandTrendsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var granularity = (request.Granularity ?? "month").ToLowerInvariant();
        var skills = await _analytics.SkillsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
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

    public async Task<Result<GeographyDto>> Handle(GetGeographicDistributionQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetGeographicDistributionQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var postings = await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
        var registrations = await _analytics.RegistrationsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
        return new GeographyDto(range.From, range.To, Cells(postings.Select(p => p.Location)),
            Cells(registrations.Where(r => r.Milestone == "ProfileCreated").Select(r => r.Governorate)));
    }

    /// <summary>Missing locations are counted under "unspecified" (RP.Geo.UNSPECIFIED_BUCKET); cells below MinCell are suppressed, not dropped.</summary>
    private IReadOnlyList<GeographyRowDto> Cells(IEnumerable<string?> locations) =>
        locations.GroupBy(StatisticsCalculator.Bucket).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => _policy.IsSuppressed(g.Count()) ? new GeographyRowDto(g.Key, null, true) : new GeographyRowDto(g.Key, g.Count(), false)).ToList();

    public async Task<Result<SalaryAnalyticsDto>> Handle(GetSalaryAnalyticsQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetSalaryAnalyticsQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var by = (request.By ?? "industry").ToLowerInvariant();
        var withSalary = (await _analytics.PostingsAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct))
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

    public async Task<Result<EmploymentOutcomesDto>> Handle(GetEmploymentOutcomesQuery request, CancellationToken ct)
    {
        if ((await Guard(nameof(GetEmploymentOutcomesQuery), ct)) is { IsFailure: true } denied)
        {
            return denied.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var rows = await _analytics.OutcomesAsync(DateRanges.StartUtc(range.From), DateRanges.EndUtc(range.To), Max, ct);
        // RP.Outcome.EXCLUDE_NO_FOLLOWUP: records without follow-up data are excluded, never filled with placeholders.
        var withFollowUp = rows.Where(r => r.FollowUps > 0).OrderBy(r => r.FirstShortlistedAtUtc).ToList();
        var items = withFollowUp.Select(r => new OutcomeItemDto(r.JobPostingId, r.Shortlisted, r.FollowUps, r.FitCount == 0 ? null : Math.Round(r.FitSum / r.FitCount, 4))).ToList();
        return new EmploymentOutcomesDto(range.From, range.To, items, rows.Count - withFollowUp.Count);
    }
}

/// <summary>Placeholder success type of the internal guard helper (keeps the handlers free of the Unit alias).</summary>
internal readonly record struct Unit2
{
    public static readonly Unit2 Value = default;
}
