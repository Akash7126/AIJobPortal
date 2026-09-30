using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Queries.ReportLibrary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/saved")]
[Authorize]
public sealed class SavedReportsController : ApiControllerBase
{
    public sealed record SaveBody(string Name, ReportDefinitionDto Definition);

    public sealed record RunSavedBody(Dictionary<string, string>? Arguments, string? Target);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        var query = new ListSavedReportsQuery();
        return Send(query, ct);
    }

    [HttpPost]
    public Task<IActionResult> Save([FromBody] SaveBody body, CancellationToken ct)
    {
        var command = new SaveReportCommand(body.Name, body.Definition);
        return SendCreated(command, r => $"/api/v1/admin/reports/saved/{r.Id}", ct);
    }

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeleteSavedReportCommand(id);
        return SendNoContent(command, ct);
    }

    [HttpPost("{id:guid}/run")]
    public Task<IActionResult> Run(Guid id, [FromBody] RunSavedBody? body, CancellationToken ct)
    {
        var command = new RunSavedReportCommand(id, body?.Arguments, body?.Target);
        return Send(command, ct);
    }
}
