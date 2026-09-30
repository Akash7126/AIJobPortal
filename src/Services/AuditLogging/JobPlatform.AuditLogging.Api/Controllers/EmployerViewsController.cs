using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.BuildingBlocks.Api.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AuditLogging.Api.Controllers;

/// <summary>Employer views: dashboard, status history of an own posting, candidate insight.</summary>
[Route("api/v1")]
[Authorize]
public sealed class EmployerViewsController : ApiControllerBase
{
    [HttpGet("employers/me/dashboard")]
    public Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var query = new GetEmployerDashboardQuery();
        return Send(query, ct);
    }

    [HttpGet("jobs/{jobPostingId:guid}/status-history")]
    public Task<IActionResult> StatusHistory(Guid jobPostingId, CancellationToken ct)
    {
        var query = new GetJobStatusHistoryQuery(jobPostingId);
        return Send(query, ct);
    }

    [HttpGet("employers/me/candidates/{candidateId:guid}/insight")]
    public Task<IActionResult> Insight(Guid candidateId, [FromQuery] Guid jobPostingId, CancellationToken ct)
    {
        var query = new GetCandidateInsightQuery(candidateId, jobPostingId);
        return Send(query, ct);
    }
}
