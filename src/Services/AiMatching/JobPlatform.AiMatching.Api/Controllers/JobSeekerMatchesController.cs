using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Application.Queries.Recommendations;
using JobPlatform.BuildingBlocks.Api.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AiMatching.Api.Controllers;

/// <summary>Job seeker views: match ranking, single score with breakdown, recommendations (US-3.3.1-01/05/06, US-3.3.2-01).</summary>
[Route("api/v1")]
[Authorize]
public sealed class JobSeekerMatchesController : ApiControllerBase
{
    [HttpGet("matches/jobs")]
    public Task<IActionResult> Ranking([FromQuery] decimal? minScore, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new GetJobMatchRankingQuery(minScore, page, pageSize), ct);
    }

    [HttpGet("matches/jobs/{jobPostingId:guid}")]
    public Task<IActionResult> Score(Guid jobPostingId, CancellationToken ct)
    {
        return Send(new GetMatchScoreQuery(jobPostingId), ct);
    }

    [HttpGet("recommendations/jobs")]
    public Task<IActionResult> Recommendations(CancellationToken ct)
    {
        return Send(new GetJobRecommendationsQuery(), ct);
    }
}
