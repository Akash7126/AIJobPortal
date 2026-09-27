using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AuditLogging.Api.Controllers;

/// <summary>Administrator report exports (3.1.4-10): 202 with a job, or 200 with the job an identical request already started.</summary>
[Route("api/v1/admin/reports/exports")]
[Authorize]
public sealed class ReportExportsController : ApiControllerBase
{
    public sealed record ExportRequest(string ReportType, string Format, Dictionary<string, string>? Parameters);

    [HttpPost]
    public Task<IActionResult> RequestExport([FromBody] ExportRequest body, CancellationToken ct)
    {
        return Send(new RequestAdministratorReportExportCommand(body.ReportType, body.Format, body.Parameters), result =>
        {
            return result.Reused ? Ok(result.Job) : Accepted($"/api/v1/admin/reports/exports/{result.Job.Id}", result.Job);
        }, ct);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetExportJobQuery(id), ct);
    }
}
