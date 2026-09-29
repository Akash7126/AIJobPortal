using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.Exports;
using JobPlatform.Reporting.Application.Queries.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

/// <summary>Exports (US-3.5.4-05): POST answers 202 with the job, also when an identical export is already running (the existing job is returned, reused = true).</summary>
[Route("api/v1/admin/reports/exports")]
[Authorize]
public sealed class ExportsController : ApiControllerBase
{
    public sealed record ExportBody(string RefKind, Guid? RefId, Dictionary<string, string>? Parameters, string Format);

    [HttpPost]
    public Task<IActionResult> RequestExport([FromBody] ExportBody body, CancellationToken ct) =>
        Send(new RequestReportExportCommand(body.RefKind, body.RefId, body.Parameters, body.Format), e => Accepted($"/api/v1/admin/reports/exports/{e.Id}", e), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Send(new GetReportExportQuery(id), ct);

    [HttpGet("{id:guid}/file")]
    public Task<IActionResult> DownloadFile(Guid id, CancellationToken ct) =>
        Send(new DownloadReportExportQuery(id), f => base.File(f.Content, f.ContentType, f.FileName), ct);
}
