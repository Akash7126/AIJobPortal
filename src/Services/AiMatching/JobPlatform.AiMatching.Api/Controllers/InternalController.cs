using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AiMatching.Api.Controllers;

[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalController : ApiControllerBase
{
    [HttpGet("match-ranking")]
    public Task<IActionResult> Ranking([FromQuery] Guid profileId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new GetJobMatchRankingForProfileQuery(profileId, page, pageSize);
        return Send(query, ct);
    }

    [HttpGet("match-scores")]
    public Task<IActionResult> Scores([FromQuery] Guid jobPostingId, [FromQuery] decimal? min, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = new ListMatchScoresQuery(jobPostingId, min, page, pageSize);
        return Send(query, ct);
    }

    [HttpGet("resume-parsed-data/{id:guid}")]
    public Task<IActionResult> ResumeParsedData(Guid id, CancellationToken ct)
    {
        var query = new GetResumeParsedDataQuery(id);
        return Send(query, ct);
    }
}
