using JobPlatform.CandidateSourcing.Application.Commands.Threshold;
using JobPlatform.CandidateSourcing.Application.Queries.Threshold;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.CandidateSourcing.Api.Controllers;

/// <summary>US-3.3.3-03: employer-set qualification cutoff for one posting.</summary>
public sealed class ThresholdController : CandidateSourcingControllerBase
{
    public sealed record SetThresholdRequest(int Percent);

    [HttpGet("jobs/{jobPostingId:guid}/qualification-threshold")]
    public Task<IActionResult> Get(Guid jobPostingId, CancellationToken ct)
    {
        var query = new GetQualificationThresholdQuery(jobPostingId);
        return Send(query, ct);
    }

    [HttpPut("jobs/{jobPostingId:guid}/qualification-threshold")]
    public Task<IActionResult> Set(Guid jobPostingId, [FromBody] SetThresholdRequest body, CancellationToken ct)
    {
        var command = new SetQualificationThresholdCommand(jobPostingId, body.Percent);
        return Send(command, ct);
    }
}
