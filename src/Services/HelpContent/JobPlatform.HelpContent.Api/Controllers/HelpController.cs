using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.HelpContent.Application.Commands.Feedback;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.HelpContent.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

[Route("api/v1/help")]
public sealed class HelpController : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> Center([FromQuery] HelpRole? role, CancellationToken ct)
    {
        return Send(new GetHelpCenterQuery(role), ct);
    }

    [HttpGet("search")]
    public Task<IActionResult> Search([FromQuery] string q, [FromQuery] HelpRole? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Send(new SearchHelpContentQuery(q, role, page, pageSize), ct);
    }

    /// <summary>US-3.7.2-04: an unmapped page key returns 204 (ASP.NET Core turns a null Ok() body into 204 automatically) - the client
    /// falls back to the general help center (AC-02); this is never an error.</summary>
    [HttpGet("context")]
    public Task<IActionResult> Context([FromQuery] string pageKey, CancellationToken ct)
    {
        return Send(new GetContextHelpQuery(pageKey), Ok, ct);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetHelpContentQuery(id), value =>
        {
            SetETag(value.RowVersion);
            return Ok(value);
        }, ct);
    }

    public sealed record FeedbackRequest(FeedbackRating Rating, string? Comment);

    /// <summary>US-3.7.2-06: upsert - a repeat replaces the prior rating (INV-12). 200 per handover section 6.1.</summary>
    [HttpPost("{id:guid}/feedback")]
    [Authorize(Policy = Policies.Authenticated)]
    public Task<IActionResult> SubmitFeedback(Guid id, [FromBody] FeedbackRequest body, CancellationToken ct)
    {
        return Send(new SubmitHelpFeedbackCommand(id, body.Rating, body.Comment), ct);
    }
}
