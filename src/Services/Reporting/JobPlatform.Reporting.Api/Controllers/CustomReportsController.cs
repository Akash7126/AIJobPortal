using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

/// <summary>Module D - custom reports (US-3.5.4). Refusals carry E-CRG-FORBIDDEN, invalid definitions E-CRG-INVALID-FIELD.</summary>
[Route("api/v1/admin/reports")]
[Authorize]
public sealed class CustomReportsController : ApiControllerBase
{
    public sealed record RunBody(Guid? TemplateId, ReportDefinitionDto? Definition, Dictionary<string, string>? Arguments, string? Target);

    public sealed record RenderBody(Guid? TemplateId, ReportDefinitionDto? Definition, Dictionary<string, string>? Arguments, string Format);

    /// <summary>200 with the result; when Power BI was requested and timed out the built-in result comes back with fallback = true and warningCode E-CRG-UPSTREAM-TIMEOUT.</summary>
    [HttpPost("custom")]
    public Task<IActionResult> Run([FromBody] RunBody body, CancellationToken ct) => Send(new RunCustomReportCommand(body.TemplateId, body.Definition, body.Arguments, body.Target), ct);

    [HttpGet("formats")]
    public Task<IActionResult> Formats(CancellationToken ct) => Send(new GetReportFormatsQuery(), ct);

    [HttpPost("render")]
    public Task<IActionResult> Render([FromBody] RenderBody body, CancellationToken ct) => Send(new RenderReportQuery(body.TemplateId, body.Definition, body.Arguments, body.Format), ct);

    [HttpPost("builder/definitions")]
    public Task<IActionResult> Build([FromBody] ReportDefinitionDto body, CancellationToken ct) => Send(new BuildReportDefinitionCommand(body), ct);

    [HttpGet("builder/fields")]
    public Task<IActionResult> Fields([FromQuery] string dataSource, CancellationToken ct) => Send(new GetReportBuilderFieldsQuery(dataSource), ct);
}
