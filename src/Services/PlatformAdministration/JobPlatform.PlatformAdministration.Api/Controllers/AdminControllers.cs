using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.PlatformAdministration.Application.Commands.Entities;
using JobPlatform.PlatformAdministration.Application.Commands.Offerings;
using JobPlatform.PlatformAdministration.Application.Commands.Reference;
using JobPlatform.PlatformAdministration.Application.Commands.Settings;
using JobPlatform.PlatformAdministration.Application.Commands.Taxonomy;
using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Queries.Entities;
using JobPlatform.PlatformAdministration.Application.Queries.Offerings;
using JobPlatform.PlatformAdministration.Application.Queries.Reference;
using JobPlatform.PlatformAdministration.Application.Queries.Settings;
using JobPlatform.PlatformAdministration.Application.Queries.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Queries.Users;
using JobPlatform.PlatformAdministration.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.PlatformAdministration.Api.Controllers;

/// <summary>Administrator control surface (Administrator role with MFA). Every refusal carries the story's code E-AUM-FORBIDDEN.</summary>
[Route("api/v1/admin")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-AUM-FORBIDDEN")]
public abstract class AdminControllerBase : ApiControllerBase
{
    protected IActionResult VersionNoContent(VersionResult result)
    {
        Response.Headers.ETag = $"\"{result.Version}\"";
        return NoContent();
    }
}

/// <summary>US-3.1.4-01: user-management list (composition of BC-03).</summary>
public sealed class UsersController : AdminControllerBase
{
    [HttpGet("users")]
    public Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? type, [FromQuery] string? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListPlatformUsersQuery(search, type, status, page, pageSize), ct);
    }
}

/// <summary>US-3.1.4-02: platform entity records.</summary>
public sealed class EntityRecordsController : AdminControllerBase
{
    public sealed record CreateEntityRecordRequest(PlatformEntityType EntityType, Dictionary<string, string>? Core);

    [HttpPost("entity-records")]
    public Task<IActionResult> Create([FromBody] CreateEntityRecordRequest body, CancellationToken ct)
    {
        return SendCreated(new CreatePlatformEntityRecordCommand(body.EntityType, body.Core, IdempotencyKey), v => $"/api/v1/admin/entity-records/{v.Id}", ct);
    }

    [HttpGet("entity-records/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetEntityRecordQuery(id), ct);
    }
}

/// <summary>US-3.1.4-06: system settings.</summary>
public sealed class SettingsController : AdminControllerBase
{
    public sealed record ChangeSettingRequest(string? Value);

    [HttpGet("settings")]
    public Task<IActionResult> List(CancellationToken ct)
    {
        return Send(new ListSystemSettingsQuery(), ct);
    }

    [HttpPut("settings/{key}")]
    public Task<IActionResult> Change(string key, [FromBody] ChangeSettingRequest body, CancellationToken ct)
    {
        return Send(new ChangeSystemSettingCommand(key, body.Value), VersionNoContent, ct);
    }
}

/// <summary>US-3.1.4-07: reference files.</summary>
public sealed class ReferenceFilesController : AdminControllerBase
{
    public sealed record UpdateReferenceFileRequest(bool ConfirmInUse, List<ReferenceChangeRequest>? Changes);

    [HttpGet("reference-files/{type}")]
    public Task<IActionResult> Get(string type, CancellationToken ct)
    {
        return Send(new GetReferenceFileQuery(type), ct);
    }

    [HttpPut("reference-files/{type}/entries")]
    public Task<IActionResult> Update(string type, [FromBody] UpdateReferenceFileRequest body, CancellationToken ct)
    {
        return Send(new UpdateReferenceFileCommand(type, body.ConfirmInUse, body.Changes), VersionNoContent, ct);
    }
}

/// <summary>US-3.1.4-08: platform taxonomies.</summary>
public sealed class TaxonomiesController : AdminControllerBase
{
    public sealed record UpdateTaxonomyRequest(List<TaxonomyChangeRequest>? Changes);

    [HttpGet("taxonomies/{type}")]
    public Task<IActionResult> Get(string type, [FromQuery] int? version, CancellationToken ct)
    {
        return Send(new GetPlatformTaxonomyQuery(type, version), view =>
        {
            Response.Headers.ETag = $"\"{view.Version}\"";
            return Ok(view);
        }, ct);
    }

    [HttpPut("taxonomies/{type}/nodes")]
    public Task<IActionResult> Update(string type, [FromBody] UpdateTaxonomyRequest body, CancellationToken ct)
    {
        return Send(new UpdatePlatformTaxonomyCommand(type, body.Changes), VersionNoContent, ct);
    }
}

/// <summary>US-3.1.4-09: moderation of job offerings.</summary>
public sealed class JobOfferingsController : AdminControllerBase
{
    public sealed record ModerationRequest(string? Reason);

    [HttpGet("job-offerings")]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListJobOfferingsQuery(status, page, pageSize), ct);
    }

    [HttpPost("job-offerings/{id:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid id, [FromBody] ModerationRequest body, CancellationToken ct)
    {
        return Send(new SuspendJobOfferingCommand(id, body.Reason), _ => NoContent(), ct);
    }

    [HttpPost("job-offerings/{id:guid}/remove")]
    public Task<IActionResult> Remove(Guid id, [FromBody] ModerationRequest body, CancellationToken ct)
    {
        return Send(new RemoveJobOfferingCommand(id, body.Reason), _ => NoContent(), ct);
    }
}
