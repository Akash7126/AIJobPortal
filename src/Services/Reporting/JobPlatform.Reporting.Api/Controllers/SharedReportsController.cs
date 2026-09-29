using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Queries.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

/// <summary>Anonymous download through the signed, expiring link of a scheduled distribution.</summary>
[Route("api/v1/reports/shared")]
public sealed class SharedReportsController : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public Task<IActionResult> Download(Guid id, [FromQuery] long expires, [FromQuery] string sig, CancellationToken ct) =>
        Send(new DownloadSharedReportQuery(id, expires, sig ?? string.Empty), f => base.File(f.Content, f.ContentType, f.FileName), ct);
}
