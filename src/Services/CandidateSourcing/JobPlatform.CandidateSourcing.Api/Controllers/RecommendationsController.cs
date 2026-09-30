using JobPlatform.CandidateSourcing.Application.Queries.Recommendations;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.CandidateSourcing.Api.Controllers;

/// <summary>US-3.3.3-01/02: recommended candidates and their stable ranking for one posting.</summary>
public sealed class RecommendationsController : CandidateSourcingControllerBase
{
    [HttpGet("jobs/{jobPostingId:guid}/candidate-recommendations")]
    public Task<IActionResult> Recommendations(Guid jobPostingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new GetCandidateRecommendationsQuery(jobPostingId, page, pageSize);
        return Send(query, ct);
    }

    [HttpGet("jobs/{jobPostingId:guid}/candidate-ranking")]
    public Task<IActionResult> Ranking(Guid jobPostingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new GetCandidateRankingQuery(jobPostingId, page, pageSize);
        return Send(query, ct);
    }
}
