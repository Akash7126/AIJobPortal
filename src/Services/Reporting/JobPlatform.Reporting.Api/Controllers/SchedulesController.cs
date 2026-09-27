using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/schedules")]
[Authorize]
public sealed class SchedulesController : ApiControllerBase
{
    public sealed record ScheduleBody(string Name, Guid? TemplateId, Guid? SavedReportId, string? Interval, string? Cron, List<string> Recipients, string Format);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) => Send(new ListSchedulesQuery(), ct);

    [HttpPost]
    public Task<IActionResult> Create([FromBody] ScheduleBody body, CancellationToken ct) =>
        SendCreated(new ConfigureReportScheduleCommand(null, body.Name, body.TemplateId, body.SavedReportId, body.Interval, body.Cron, body.Recipients, body.Format),
            s => $"/api/v1/admin/reports/schedules/{s.Id}", ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] ScheduleBody body, CancellationToken ct) =>
        Send(new ConfigureReportScheduleCommand(id, body.Name, body.TemplateId, body.SavedReportId, body.Interval, body.Cron, body.Recipients, body.Format), _ => NoContent(), ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => SendNoContent(new DeleteReportScheduleCommand(id), ct);
}
