using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Queries.Employment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/employment")]
[Authorize]
public sealed class EmploymentController : ApiControllerBase
{
    [HttpGet("statistics")]
    public Task<IActionResult> Statistics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetEmploymentStatisticsQuery(from, to);
        return Send(query, ct);
    }

    [HttpGet("metrics")]
    public Task<IActionResult> Metrics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity, CancellationToken ct)
    {
        var query = new GetEmploymentMetricsQuery(from, to, granularity);
        return Send(query, ct);
    }

    [HttpGet("industries")]
    public Task<IActionResult> Industries([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetIndustryAnalyticsQuery(from, to);
        return Send(query, ct);
    }

    [HttpGet("skills/trends")]
    public Task<IActionResult> SkillTrends([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity, CancellationToken ct)
    {
        var query = new GetSkillDemandTrendsQuery(from, to, granularity);
        return Send(query, ct);
    }

    [HttpGet("geography")]
    public Task<IActionResult> Geography([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetGeographicDistributionQuery(from, to);
        return Send(query, ct);
    }

    [HttpGet("salaries")]
    public Task<IActionResult> Salaries([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? by, CancellationToken ct)
    {
        var query = new GetSalaryAnalyticsQuery(from, to, by);
        return Send(query, ct);
    }

    [HttpGet("outcomes")]
    public Task<IActionResult> Outcomes([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetEmploymentOutcomesQuery(from, to);
        return Send(query, ct);
    }
}
