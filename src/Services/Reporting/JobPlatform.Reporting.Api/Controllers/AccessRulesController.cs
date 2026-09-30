using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.Queries.ReportRuns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/access-rules")]
[Authorize]
public sealed class AccessRulesController : ApiControllerBase
{
    public sealed record RuleBody(string Role, List<string> Categories);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        var query = new ListAccessRulesQuery();
        return Send(query, ct);
    }

    [HttpPut]
    public Task<IActionResult> Configure([FromBody] RuleBody body, CancellationToken ct)
    {
        var command = new ConfigureReportAccessCommand(body.Role, body.Categories);
        return Send(command, ct);
    }
}
