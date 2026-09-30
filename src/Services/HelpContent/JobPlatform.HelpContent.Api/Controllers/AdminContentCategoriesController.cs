using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.Queries.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

[Route("api/v1/admin/content-categories")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-NEWSU-FORBIDDEN")]
public sealed class AdminContentCategoriesController : ApiControllerBase
{
    public sealed record ContentCategoryRequest(string? NameAr, string? NameEn);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        var query = new ListContentCategoriesQuery();
        return Send(query, ct);
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] ContentCategoryRequest body, CancellationToken ct)
    {
        var command = new CreateContentCategoryCommand(body.NameAr, body.NameEn);
        return Send(command, value => Created($"/api/v1/admin/content-categories/{value.CategoryId}", value), ct);
    }

    /// <summary>Soft delete (handover section 3.2): the category's articles fall back to "uncategorized", never a broken reference.</summary>
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeleteContentCategoryCommand(id);
        return SendNoContent(command, ct);
    }
}
