using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

[Route("api/v1/admin/help-topics")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-FAQHC-FORBIDDEN")]
public sealed class AdminHelpTopicsController : ApiControllerBase
{
    public sealed record HelpTopicRequest(string? NameAr, string? NameEn);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) => Send(new ListHelpTopicsQuery(), ct);

    [HttpPost]
    public Task<IActionResult> Create([FromBody] HelpTopicRequest body, CancellationToken ct) =>
        Send(new CreateHelpTopicCommand(body.NameAr, body.NameEn), value => Created($"/api/v1/admin/help-topics/{value.TopicId}", value), ct);

    /// <summary>Soft removal (handover section 3.4, INV-11): assigned articles fall back to "uncategorized", never a broken reference.</summary>
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Remove(Guid id, CancellationToken ct) => SendNoContent(new RemoveHelpTopicCommand(id), ct);
}
