using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.HelpContent.Application.Queries.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

[Route("api/v1/news")]
public sealed class NewsController : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] Guid? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Send(new ListNewsQuery(category, page, pageSize), ct);

    [HttpGet("archive")]
    public Task<IActionResult> Archive([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Send(new SearchNewsArchiveQuery(q, page, pageSize), ct);

    /// <summary>US-3.7.1-05: personalised feed for a job seeker, else the general feed (never an error).</summary>
    [HttpGet("feed")]
    [Authorize(Policy = Policies.JobSeeker)]
    public Task<IActionResult> Feed(CancellationToken ct) => Send(new GetNewsFeedQuery(), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Send(new GetNewsArticleQuery(id), value =>
    {
        SetETag(value.RowVersion);
        return Ok(value);
    }, ct);
}
