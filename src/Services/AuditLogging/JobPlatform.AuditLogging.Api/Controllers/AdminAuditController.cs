using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.BuildingBlocks.Api.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AuditLogging.Api.Controllers;

/// <summary>Administrator audit logs (each refusal carries the story's own error code).</summary>
[Route("api/v1/admin/audit")]
[Authorize]
public sealed class AdminAuditController : ApiControllerBase
{
    [HttpGet("jobs/{platformJobId}")]
    public Task<IActionResult> JobAuditTrail(string platformJobId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        return Send(new GetJobAuditTrailQuery(platformJobId, from, to, outcome, page, pageSize, includeArchived), ct);
    }

    [HttpGet("admin-actions")]
    public Task<IActionResult> AdminActions([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        return Send(new ListAdminAuditLogQuery(from, to, outcome, page, pageSize, includeArchived), ct);
    }

    [HttpGet("access")]
    public Task<IActionResult> Access([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        return Send(new ListAccessLogQuery(from, to, outcome, page, pageSize, includeArchived), ct);
    }

    [HttpGet("government-exchanges")]
    public Task<IActionResult> GovernmentExchanges([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        return Send(new ListGovernmentDataAuditTrailQuery(from, to, outcome, page, pageSize, includeArchived), ct);
    }

    [HttpGet("emails")]
    public Task<IActionResult> Emails([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListEmailLogQuery(page, pageSize), ct);
    }

    [HttpGet("sms")]
    public Task<IActionResult> Sms([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListSmsMessageLogQuery(page, pageSize), ct);
    }
}
