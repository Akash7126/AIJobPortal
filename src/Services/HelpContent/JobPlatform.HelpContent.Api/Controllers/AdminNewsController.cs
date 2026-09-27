using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

/// <summary>Administrator authoring surface of the news CMS (handover section 6.1, stories US-3.7.1-01..04/06). Every refusal carries
/// E-NEWSU-FORBIDDEN.</summary>
[Route("api/v1/admin/news")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-NEWSU-FORBIDDEN")]
public sealed class AdminNewsController : ApiControllerBase
{
    public sealed record NewsRequest(NewsKind Kind, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn);

    /// <summary>US-3.7.1-02: 201 for a genuinely new draft, 200 when an identical draft already existed (INV-02).</summary>
    [HttpPost]
    public Task<IActionResult> Create([FromBody] NewsRequest body, CancellationToken ct) =>
        Send(new CreateNewsArticleCommand(body.Kind, body.TitleAr, body.TitleEn, body.BodyAr, body.BodyEn),
            result => result.Existing ? Ok(result.Article) : Created($"/api/v1/news/{result.Article.NewsArticleId}", result.Article), ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Edit(Guid id, [FromBody] NewsRequest body, CancellationToken ct) =>
        Send(new EditNewsArticleCommand(id, body.TitleAr, body.TitleEn, body.BodyAr, body.BodyEn), ct);

    public sealed record MediaRequest(NewsMediaType Type, string? AltText);

    /// <summary>US-3.7.1-03: 413/415 on media rules (INV-04).</summary>
    [HttpPost("{id:guid}/media")]
    [RequestSizeLimit(NewsArticle.MaxMediaSizeBytes)]
    public async Task<IActionResult> AddMedia(Guid id, IFormFile? file, [FromForm] NewsMediaType type, [FromForm] string? altText, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["file"] = new[] { "VAL.File.Required" } }).ToActionResult(HttpContext);
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return await Send(new AddNewsMediaCommand(id, type, file.FileName, file.ContentType, file.Length, stream.ToArray(), altText),
            value => Created($"/api/v1/news/{id}", value), ct);
    }

    /// <summary>US-3.7.1-01: 204 also when already published (idempotent no-op).</summary>
    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct) => SendNoContent(new PublishNewsArticleCommand(id), ct);

    /// <summary>US-3.7.1-06: 204 also when already archived (idempotent no-op).</summary>
    [HttpPost("{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) => SendNoContent(new ArchiveNewsArticleCommand(id), ct);

    public sealed record CategorizationRequest(IReadOnlyList<Guid> CategoryIds, IReadOnlyList<string> Tags);

    /// <summary>US-3.7.1-04.</summary>
    [HttpPut("{id:guid}/categorization")]
    public Task<IActionResult> UpdateCategorization(Guid id, [FromBody] CategorizationRequest body, CancellationToken ct) =>
        SendNoContent(new UpdateContentCategorizationCommand(id, body.CategoryIds, body.Tags), ct);
}
