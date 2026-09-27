using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/employment")]
[Authorize]
public sealed class EmploymentController : ApiControllerBase
{
    [HttpGet("statistics")]
    public Task<IActionResult> Statistics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Send(new GetEmploymentStatisticsQuery(from, to), ct);

    [HttpGet("metrics")]
    public Task<IActionResult> Metrics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity, CancellationToken ct) =>
        Send(new GetEmploymentMetricsQuery(from, to, granularity), ct);

    [HttpGet("industries")]
    public Task<IActionResult> Industries([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Send(new GetIndustryAnalyticsQuery(from, to), ct);

    [HttpGet("skills/trends")]
    public Task<IActionResult> SkillTrends([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity, CancellationToken ct) =>
        Send(new GetSkillDemandTrendsQuery(from, to, granularity), ct);

    [HttpGet("geography")]
    public Task<IActionResult> Geography([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Send(new GetGeographicDistributionQuery(from, to), ct);

    [HttpGet("salaries")]
    public Task<IActionResult> Salaries([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? by, CancellationToken ct) =>
        Send(new GetSalaryAnalyticsQuery(from, to, by), ct);

    [HttpGet("outcomes")]
    public Task<IActionResult> Outcomes([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Send(new GetEmploymentOutcomesQuery(from, to), ct);
}
