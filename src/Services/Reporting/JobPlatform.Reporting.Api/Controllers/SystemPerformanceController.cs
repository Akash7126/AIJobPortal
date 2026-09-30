using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.Performance;
using JobPlatform.Reporting.Application.Queries.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/system")]
[Authorize]
public sealed class SystemPerformanceController : ApiControllerBase
{
    public sealed record AlertRuleBody(Guid? Id, string Metric, string Comparator, decimal Threshold, int WindowMinutes, string Severity, bool Enabled = true);

    [HttpGet("performance")]
    public Task<IActionResult> Performance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetSystemPerformanceQuery(from, to);
        return Send(query, ct);
    }

    [HttpGet("usage")]
    public Task<IActionResult> Usage([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetUsagePatternsQuery(from, to);
        return Send(query, ct);
    }

    [HttpGet("dashboard")]
    public Task<IActionResult> Dashboard([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = new GetPerformanceDashboardQuery(from, to);
        return Send(query, ct);
    }

    [HttpGet("history")]
    public Task<IActionResult> History([FromQuery] string metric, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity, CancellationToken ct)
    {
        var query = new GetMetricHistoryQuery(metric, from, to, granularity);
        return Send(query, ct);
    }

    [HttpGet("alert-rules")]
    public Task<IActionResult> AlertRules(CancellationToken ct)
    {
        var query = new ListAlertRulesQuery();
        return Send(query, ct);
    }

    [HttpPut("alert-rules")]
    public Task<IActionResult> ConfigureAlertRule([FromBody] AlertRuleBody body, CancellationToken ct)
    {
        var command = new ConfigurePerformanceAlertRuleCommand(body.Id, body.Metric, body.Comparator, body.Threshold, body.WindowMinutes, body.Severity, body.Enabled);
        return Send(command, ct);
    }

    [HttpGet("alerts")]
    public Task<IActionResult> Alerts([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var query = new ListAlertsQuery(take);
        return Send(query, ct);
    }
}
