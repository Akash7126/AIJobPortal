using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.Schedules;
using JobPlatform.Reporting.Application.Queries.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/schedules")]
[Authorize]
public sealed class SchedulesController : ApiControllerBase
{
    public sealed record ScheduleBody(string Name, Guid? TemplateId, Guid? SavedReportId, string? Interval, string? Cron, List<string> Recipients, string Format);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        var query = new ListSchedulesQuery();
        return Send(query, ct);
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] ScheduleBody body, CancellationToken ct)
    {
        var command = new ConfigureReportScheduleCommand(null, body.Name, body.TemplateId, body.SavedReportId, body.Interval, body.Cron, body.Recipients, body.Format);
        return SendCreated(command,
            s => $"/api/v1/admin/reports/schedules/{s.Id}", ct);
    }

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] ScheduleBody body, CancellationToken ct)
    {
        var command = new ConfigureReportScheduleCommand(id, body.Name, body.TemplateId, body.SavedReportId, body.Interval, body.Cron, body.Recipients, body.Format);
        return Send(command, _ => NoContent(), ct);
    }

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeleteReportScheduleCommand(id);
        return SendNoContent(command, ct);
    }
}
