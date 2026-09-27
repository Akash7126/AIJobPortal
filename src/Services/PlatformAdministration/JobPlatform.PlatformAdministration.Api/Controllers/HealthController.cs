using JobPlatform.BuildingBlocks.Api.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.PlatformAdministration.Api.Controllers;

[Route("api/v1/health")]
public sealed class HealthController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { Status = "Healthy" });
    }
}
