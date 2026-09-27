using JobPlatform.AiMatching.Application;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AiMatching.Api.Controllers;

/// <summary>Administrator matching configuration: threshold and criterion weights (US-3.3.1-04/07). Updates honour If-Match.</summary>
[Route("api/v1/admin/matching-configuration")]
[Authorize]
public sealed class MatchingConfigurationController : ApiControllerBase
{
    public sealed record ThresholdRequest(decimal ThresholdPercent);

    public sealed record WeightsRequest(decimal SkillOverlap, decimal Education, decimal Training, decimal Location, decimal Experience, decimal Salary);

    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct)
    {
        return Send(new GetMatchingConfigurationQuery(), dto =>
        {
            Response.Headers.ETag = dto.ETag;
            return Ok(dto);
        }, ct);
    }

    [HttpPut("threshold")]
    public Task<IActionResult> Threshold([FromBody] ThresholdRequest body, CancellationToken ct)
    {
        return SendNoContent(new ConfigureMatchThresholdCommand(body.ThresholdPercent, IfMatch), ct);
    }

    [HttpPut("weights")]
    public Task<IActionResult> Weights([FromBody] WeightsRequest body, CancellationToken ct)
    {
        return SendNoContent(new ConfigureMatchingParameterCommand(body.SkillOverlap, body.Education, body.Training, body.Location, body.Experience, body.Salary, IfMatch), ct);
    }
}
