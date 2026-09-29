using JobPlatform.CandidateSourcing.Application.Commands.TalentPool;
using JobPlatform.CandidateSourcing.Application.Queries.TalentPool;
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
