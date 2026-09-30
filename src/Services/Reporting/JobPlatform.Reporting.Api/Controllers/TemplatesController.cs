using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Application.Queries.ReportLibrary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/templates")]
[Authorize]
public sealed class TemplatesController : ApiControllerBase
{
    public sealed record TemplateBody(string Name, string DataSource, List<TemplateParameterDto>? Parameters);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        return Send(new ListTemplatesQuery(), ct);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetTemplateQuery(id), ct);
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] TemplateBody body, CancellationToken ct)
    {
        return SendCreated(new SaveReportTemplateCommand(null, body.Name, body.DataSource, body.Parameters), t => $"/api/v1/admin/reports/templates/{t.Id}", ct);
    }

    /// <summary>Later save wins (AC-04): no If-Match is required.</summary>
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] TemplateBody body, CancellationToken ct)
    {
        return Send(new SaveReportTemplateCommand(id, body.Name, body.DataSource, body.Parameters), _ => NoContent(), ct);
    }
}
