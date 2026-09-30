using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.Activity;
using JobPlatform.Reporting.Application.Queries.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

/// <summary>Module A - user activity monitoring (US-3.5.1). Administrator only; refusals carry E-UAM-FORBIDDEN.</summary>
[Route("api/v1/admin/reports")]
[Authorize]
public sealed class ActivityController : ApiControllerBase
{
    public sealed record RetentionBody(int Months);

    [HttpGet("activity")]
    public Task<IActionResult> Activity([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? type, CancellationToken ct)
    {
        var query = new GetUserActivityQuery(from, to, type);
        return Send(query, ct);
    }

    [HttpGet("activity/retention-policy")]
    public Task<IActionResult> GetRetention(CancellationToken ct)
    {
        var query = new GetRetentionPolicyQuery();
        return Send(query, ct);
    }

    [HttpPut("activity/retention-policy")]
    public Task<IActionResult> SetRetention([FromBody] RetentionBody body, CancellationToken ct)
    {
        var command = new SetActivityRetentionPolicyCommand(body.Months);
        return Send(command, _ => NoContent(), ct);
    }

    [HttpGet("logins/current")]
    public Task<IActionResult> Logins(CancellationToken ct)
    {
        var query = new GetLoginDashboardQuery();
        return Send(query, ct);
    }
}
