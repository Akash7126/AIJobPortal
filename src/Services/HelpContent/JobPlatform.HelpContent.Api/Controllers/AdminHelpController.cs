using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.Queries.Feedback;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

/// <summary>Administrator authoring surface of the FAQ/help center (handover section 6.1, stories US-3.7.2-02/04/05/08). Every refusal
/// carries E-FAQHC-FORBIDDEN.</summary>
[Route("api/v1/admin/help")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-FAQHC-FORBIDDEN")]
public sealed class AdminHelpController : ApiControllerBase
{
    public sealed record HelpContentRequest(HelpKind Kind, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn);

    /// <summary>US-3.7.2-05.</summary>
    [HttpPost]
    public Task<IActionResult> Create([FromBody] HelpContentRequest body, CancellationToken ct)
    {
        return Send(new CreateHelpContentCommand(body.Kind, body.TitleAr, body.TitleEn, body.BodyAr, body.BodyEn),
            value => Created($"/api/v1/help/{value.HelpContentId}", value), ct);
    }

    /// <summary>US-3.7.2-05: every save creates a new version atomically; concurrent edits are "later save wins" (AC-03).
    /// </summary>
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] HelpContentRequest body, CancellationToken ct)
    {
        return Send(new UpdateHelpContentCommand(id, body.TitleAr, body.TitleEn, body.BodyAr, body.BodyEn), ct);
    }

    public sealed record OrganizationRequest(Guid? TopicId, IReadOnlyList<HelpRole> Roles);

    /// <summary>US-3.7.2-02.</summary>
    [HttpPut("{id:guid}/organization")]
    public Task<IActionResult> UpdateOrganization(Guid id, [FromBody] OrganizationRequest body, CancellationToken ct)
    {
        return SendNoContent(new UpdateHelpContentOrganizationCommand(id, body.TopicId, body.Roles), ct);
    }

    public sealed record MediaRequest(HelpMediaType Type, string? CaptionsRef, string? TextAlternative);

    /// <summary>US-3.7.2-08: 415 when captions/text-alternative are missing (INV-10).</summary>
    [HttpPost("{id:guid}/media")]
    public async Task<IActionResult> AttachMedia(Guid id, IFormFile? file, [FromForm] HelpMediaType type, [FromForm] string? captionsRef,
        [FromForm] string? textAlternative, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["file"] = new[] { "VAL.File.Required" } }).ToActionResult(HttpContext);
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return await Send(new AttachHelpMediaCommand(id, type, file.FileName, file.ContentType, file.Length, stream.ToArray(), captionsRef, textAlternative),
            value => Created($"/api/v1/help/{id}", value), ct);
    }

    /// <summary>US-3.7.2-06 AC-03: aggregate feedback is administrator-only.</summary>
    [HttpGet("feedback/summary")]
    public Task<IActionResult> FeedbackSummary([FromQuery] Guid helpContentId, CancellationToken ct)
    {
        return Send(new GetHelpFeedbackSummaryQuery(helpContentId), ct);
    }

    /// <summary>US-3.7.2-04. The page key is a catch-all route segment: keys look like "employer/dashboard" (handover section 3.8),
    /// which would otherwise split across path segments.</summary>
    [HttpPut("context-mappings/{*pageKey}")]
    public Task<IActionResult> SetContextMapping(string pageKey, [FromBody] Guid helpContentId, CancellationToken ct)
    {
        return SendNoContent(new SetContextHelpMappingCommand(pageKey, helpContentId), ct);
    }
}
