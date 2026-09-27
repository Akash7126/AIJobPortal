using JobPlatform.AiMatching.Application;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AiMatching.Api.Controllers;

/// <summary>Employer views of an owned posting: reverse matches, async shortlist, candidate recommendations (US-3.3.1-02/06, US-3.3.2-02).</summary>
[Route("api/v1/employers/jobs/{jobPostingId:guid}")]
[Authorize]
public sealed class EmployerMatchesController : ApiControllerBase
{
    public sealed record ShortlistRequest(int? Size);

    [HttpGet("candidates")]
    public Task<IActionResult> Candidates(Guid jobPostingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new GetReverseMatchesQuery(jobPostingId, page, pageSize), ct);
    }

    [HttpGet("candidate-recommendations")]
    public Task<IActionResult> CandidateRecommendations(Guid jobPostingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new GetCandidateRecommendationsQuery(jobPostingId, page, pageSize), ct);
    }

    /// <summary>Batch work: 202 with the queued shortlist and a Location to poll.</summary>
    [HttpPost("shortlists")]
    public Task<IActionResult> RequestShortlist(Guid jobPostingId, [FromBody] ShortlistRequest? body, CancellationToken ct)
    {
        return Send(new ComputeCandidateShortlistCommand(jobPostingId, body?.Size), s => Accepted($"/api/v1/employers/jobs/{jobPostingId}/shortlists/{s.Id}", s), ct);
    }

    [HttpGet("shortlists/{shortlistId:guid}")]
    public Task<IActionResult> Shortlist(Guid jobPostingId, Guid shortlistId, CancellationToken ct)
    {
        return Send(new GetCandidateShortlistQuery(jobPostingId, shortlistId), ct);
    }
}
