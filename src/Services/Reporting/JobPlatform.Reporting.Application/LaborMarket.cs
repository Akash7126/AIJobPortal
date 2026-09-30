using System.Text.Json;
using FluentValidation;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application;

/// <summary>Builds the content of a labor-market report from the analytics store (counts only; every breakdown honours small-cell suppression).</summary>
public sealed class LaborMarketReportBuilder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAnalyticsQueryService _analytics;
    private readonly StatisticsPolicy _policy;

    public LaborMarketReportBuilder(IAnalyticsQueryService analytics, StatisticsPolicy policy)
    {
        _analytics = analytics;
        _policy = policy;
    }

    public async Task<string> BuildJsonAsync(string period, CancellationToken ct)
    {
        LaborMarketReport.TryParsePeriod(period, out var start, out var end);
        var postings = await _analytics.PostingsAsync(start, end, 200_000, ct);
        var registrations = await _analytics.RegistrationsAsync(start, end, 200_000, ct);
        var skills = await _analytics.SkillsAsync(start, end, 200_000, ct);

        IReadOnlyList<object> Top(IEnumerable<string?> values) => values.GroupBy(StatisticsCalculator.Bucket)
            .Where(g => !_policy.IsSuppressed(g.Count())).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(10)
            .Select(g => (object)new { name = g.Key, count = g.Count() }).ToList();

        var withSalary = postings.Where(p => p.SalaryMin is not null || p.SalaryMax is not null).ToList();
        var content = new
        {
            period,
            insufficientData = _policy.IsInsufficient(postings.Count),
            postings = new { total = postings.Count, active = postings.Count(p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase)) },
            topIndustries = Top(postings.Select(p => p.Category)),
            topLocations = Top(postings.Select(p => p.Location)),
            registrations = new
            {
                jobSeekers = registrations.Count(r => r.Milestone == "AccountCreated" && r.ActorType == "JobSeeker"),
                employers = registrations.Count(r => r.Milestone == "AccountCreated" && r.ActorType == "Employer")
            },
            topSkillsInDemand = skills.Where(s => s.Side == FactSkillDemand.Demand).GroupBy(s => s.Skill).Where(g => !_policy.IsSuppressed(g.Count()))
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(10).Select(g => new { name = g.Key, count = g.Count() }).ToList(),
            salary = _policy.IsInsufficient(withSalary.Count)
                ? null
                : new { samples = withSalary.Count, average = Math.Round(withSalary.Average(p => StatisticsCalculator.Midpoint(p.SalaryMin, p.SalaryMax)), 2) }
        };
        return JsonSerializer.Serialize(content, Json);
    }
}
