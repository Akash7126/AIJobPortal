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

/// <summary>US-3.3.3-07: the employer's saved candidates.</summary>
public sealed class TalentPoolController : CandidateSourcingControllerBase
{
    public sealed record AddRequest(Guid CandidateProfileId, Guid JobPostingId, string? Note);

    [HttpPost("talent-pool")]
    public Task<IActionResult> Add([FromBody] AddRequest body, CancellationToken ct) =>
        SendCreated(new AddToTalentPoolCommand(body.CandidateProfileId, body.JobPostingId, body.Note), v => $"/api/v1/employers/me/talent-pool/{v.TalentPoolEntryId}", ct);

    [HttpGet("talent-pool")]
    public Task<IActionResult> List(CancellationToken ct) => Send(new ListTalentPoolQuery(), ct);

    [HttpDelete("talent-pool/{id:guid}")]
    public Task<IActionResult> Remove(Guid id, CancellationToken ct) => SendNoContent(new RemoveFromTalentPoolCommand(id), ct);
}
