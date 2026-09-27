using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.JobPosting.Application;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.JobPosting.Api.Controllers;

/// <summary>Service-to-service routes (handover section 6.1/6.2): client-credentials token, scope identity.internal, never exposed by the gateway.</summary>
[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalController : ApiControllerBase
{
    /// <summary>Implements <see cref="IJobPostingApi"/> for BC-10/BC-11.</summary>
    [HttpGet("postings/{id:guid}")]
    public async Task<IActionResult> GetPostingForMatching(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new GetPostingForMatchingQuery(id), ct);
        if (result.IsFailure)
        {
            return result.Error!.ToActionResult(HttpContext);
        }

        return result.Value is null ? NotFound() : Ok(result.Value);
    }

    /// <summary>US-3.2.4-02 backfill: BC-07 holds the authoritative audited status history; this BC keeps no separate local trace (see status doc).</summary>
    [HttpGet("postings/{id:guid}/status-history")]
    public IActionResult GetStatusHistory(Guid id) => Ok(Array.Empty<object>());

    /// <summary>US-3.1.2-05 (BC-06 Q-05): open postings of an employer, for the employer's public company profile page.</summary>
    [HttpGet("employers/{id:guid}/open-postings")]
    public Task<IActionResult> ListOpenPostings(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Send(new ListOpenPostingsByEmployerQuery(id, page, pageSize), ct);

    /// <summary>US-3.1.4-07 (INV-04): which of these reference codes (skills/jobs/trainings) does BC-09 still use?</summary>
    [HttpPost("reference-usage/check")]
    public async Task<IActionResult> CheckReferenceUsage([FromBody] ReferenceUsageCheckRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new CheckReferenceUsageQuery(body.Type, body.Codes), ct);
        return result.ToActionResult(HttpContext, inUse => Ok(new ReferenceUsageCheckResponse(inUse.ToArray())));
    }
}
