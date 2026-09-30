using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Reporting.Application.Queries.ODataFeed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Reporting.Api.Controllers;

/// <summary>Power BI feed: read-only OData-style views for a service principal (handover 6.1, Q-10). 401 without a token, 403 (E-CRG-FORBIDDEN) for any other caller.</summary>
[Route("odata/v1")]
[Authorize]
public sealed class ODataController : ApiControllerBase
{
    [HttpGet]
    public IActionResult ServiceDocument()
    {
        return Ok(new { context = "/odata/v1/$metadata", value = GetODataViewQuery.Views.Keys.Select(name => new { name, kind = "EntitySet", url = name }) });
    }

    [HttpGet("{view}")]
    public Task<IActionResult> View(string view, [FromQuery(Name = "$top")] int top = 1000, CancellationToken ct = default)
    {
        return Send(new GetODataViewQuery(view, top), table => Ok(new Dictionary<string, object?>
        {
            ["@odata.context"] = $"/odata/v1/$metadata#{view}",
            ["value"] = table.Rows.Select(row => table.Columns.Select((c, i) => (c.Name, Value: row[i])).ToDictionary(x => x.Name, x => x.Value)).ToList()
        }), ct);
    }
}
