using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Queries.ReportRuns;
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
    public Task<IActionResult> Run([FromBody] RunBody body, CancellationToken ct)
    {
        var command = new RunCustomReportCommand(body.TemplateId, body.Definition, body.Arguments, body.Target);
        return Send(command, ct);
    }

    [HttpGet("formats")]
    public Task<IActionResult> Formats(CancellationToken ct)
    {
        var query = new GetReportFormatsQuery();
        return Send(query, ct);
    }

    [HttpPost("render")]
    public Task<IActionResult> Render([FromBody] RenderBody body, CancellationToken ct)
    {
        var query = new RenderReportQuery(body.TemplateId, body.Definition, body.Arguments, body.Format);
        return Send(query, ct);
    }

    [HttpPost("builder/definitions")]
    public Task<IActionResult> Build([FromBody] ReportDefinitionDto body, CancellationToken ct)
    {
        var command = new BuildReportDefinitionCommand(body);
        return Send(command, ct);
    }

    [HttpGet("builder/fields")]
    public Task<IActionResult> Fields([FromQuery] string dataSource, CancellationToken ct)
    {
        var query = new GetReportBuilderFieldsQuery(dataSource);
        return Send(query, ct);
    }
}
