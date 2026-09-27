using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.CandidateSourcing.Application;
using JobPlatform.CandidateSourcing.Application.Insight;
using JobPlatform.CandidateSourcing.Application.Recommendations;
using JobPlatform.CandidateSourcing.Application.Search;
using JobPlatform.CandidateSourcing.Application.TalentPool;
using JobPlatform.CandidateSourcing.Application.Threshold;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.CandidateSourcing.Api.Controllers;

/// <summary>US-3.3.3-03: employer-set qualification cutoff for one posting.</summary>
public sealed class ThresholdController : CandidateSourcingControllerBase
{
    public sealed record SetThresholdRequest(int Percent);

    [HttpGet("jobs/{jobPostingId:guid}/qualification-threshold")]
    public Task<IActionResult> Get(Guid jobPostingId, CancellationToken ct) => Send(new GetQualificationThresholdQuery(jobPostingId), ct);

    [HttpPut("jobs/{jobPostingId:guid}/qualification-threshold")]
    public Task<IActionResult> Set(Guid jobPostingId, [FromBody] SetThresholdRequest body, CancellationToken ct) =>
        Send(new SetQualificationThresholdCommand(jobPostingId, body.Percent), ct);
}
