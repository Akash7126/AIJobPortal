using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Commands.LaborMarket;
using JobPlatform.Reporting.Application.Queries.LaborMarket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

[Route("api/v1/admin/reports/labor-market")]
[Authorize]
public sealed class LaborMarketController : ApiControllerBase
{
    public sealed record GenerateBody(string? Period);

    [HttpGet]
    public Task<IActionResult> Get([FromQuery] string? period, CancellationToken ct)
    {
        var query = new GetLaborMarketReportQuery(period);
        return Send(query, ct);
    }

    [HttpPost("generate")]
    public Task<IActionResult> Generate([FromBody] GenerateBody? body, CancellationToken ct)
    {
        var command = new GenerateLaborMarketReportCommand(body?.Period);
        return Send(command, ct);
    }
}
