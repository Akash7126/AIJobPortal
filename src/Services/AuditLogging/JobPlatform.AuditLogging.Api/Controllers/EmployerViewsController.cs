using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
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
        return Send(new GetEmployerDashboardQuery(), ct);
    }

    [HttpGet("jobs/{jobPostingId:guid}/status-history")]
    public Task<IActionResult> StatusHistory(Guid jobPostingId, CancellationToken ct)
    {
        return Send(new GetJobStatusHistoryQuery(jobPostingId), ct);
    }

    [HttpGet("employers/me/candidates/{candidateId:guid}/insight")]
    public Task<IActionResult> Insight(Guid candidateId, [FromQuery] Guid jobPostingId, CancellationToken ct)
    {
        return Send(new GetCandidateInsightQuery(candidateId, jobPostingId), ct);
    }
}
