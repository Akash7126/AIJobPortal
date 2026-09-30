using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.BuildingBlocks.Api.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AuditLogging.Api.Controllers;

/// <summary>Partner-facing logs and dashboards (own data only; the route is "me", the scope is the caller).</summary>
[Route("api/v1/partners/me")]
[Authorize]
public sealed class PartnerLogsController : ApiControllerBase
{
    [HttpGet("api-responses")]
    public Task<IActionResult> ApiResponses([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var query = new ListApiResponseLogQuery(from, to, outcome, page, pageSize, includeArchived);
        return Send(query, ct);
    }

    [HttpGet("submissions")]
    public Task<IActionResult> Submissions([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var query = new ListSubmissionLogQuery(from, to, outcome, page, pageSize, includeArchived);
        return Send(query, ct);
    }

    [HttpGet("sync-errors")]
    public Task<IActionResult> SyncErrors([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var query = new ListSyncErrorLogQuery(from, to, outcome, page, pageSize, includeArchived);
        return Send(query, ct);
    }

    [HttpGet("sync-dashboard")]
    public Task<IActionResult> SyncDashboard([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new GetSyncDashboardQuery(page, pageSize);
        return Send(query, ct);
    }

    [HttpGet("integration-status")]
    public Task<IActionResult> IntegrationStatus(CancellationToken ct)
    {
        var query = new GetIntegrationStatusDashboardQuery();
        return Send(query, ct);
    }

    [HttpGet("usage-statistics")]
    public Task<IActionResult> UsageStatistics([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        var query = new GetIntegrationUsageStatisticsQuery(from, to);
        return Send(query, ct);
    }
}
