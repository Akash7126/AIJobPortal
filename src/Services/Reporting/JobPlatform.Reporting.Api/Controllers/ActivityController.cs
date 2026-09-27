using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application;
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
    public Task<IActionResult> Activity([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? type, CancellationToken ct) =>
        Send(new GetUserActivityQuery(from, to, type), ct);

    [HttpGet("activity/retention-policy")]
    public Task<IActionResult> GetRetention(CancellationToken ct) => Send(new GetRetentionPolicyQuery(), ct);

    [HttpPut("activity/retention-policy")]
    public Task<IActionResult> SetRetention([FromBody] RetentionBody body, CancellationToken ct) =>
        Send(new SetActivityRetentionPolicyCommand(body.Months), _ => NoContent(), ct);

    [HttpGet("logins/current")]
    public Task<IActionResult> Logins(CancellationToken ct) => Send(new GetLoginDashboardQuery(), ct);
}
