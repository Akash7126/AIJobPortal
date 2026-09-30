using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.PlatformAdministration.Application.Queries.Reference;
using JobPlatform.PlatformAdministration.Application.Queries.Settings;
using JobPlatform.PlatformAdministration.Application.Queries.Taxonomy;
using JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.PlatformAdministration.Api.Controllers;

/// <summary>
/// Service-to-service reads of the reference-data authority (/internal/v1, client-credentials token with the internal scope). The taxonomy answer follows the
/// shared contract <see cref="TaxonomyDto"/>; the ETag is the taxonomy version.
/// </summary>
[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalController : ApiControllerBase
{
    [HttpGet("taxonomies/{type}")]
    public async Task<IActionResult> Taxonomy(string type, [FromQuery] string? version, CancellationToken ct)
    {
        int? requested = null;
        if (version is not null)
        {
            if (!int.TryParse(version.Trim('"'), out var parsed) || parsed < 1)
            {
                return NotFound(); // an unknown version is a version that does not exist
            }

            requested = parsed;
        }

        return await Send(new GetTaxonomyForConsumersQuery(type, requested), view =>
        {
            Response.Headers.ETag = $"\"{view.Version}\"";
            return Ok(new TaxonomyDto(view.Type, view.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                view.Nodes.Select(n => new TaxonomyEntryDto(n.Code, n.Name.En, n.Name.Ar, n.Synonyms, n.IsActive, n.ParentCode)).ToList()));
        }, ct);
    }

    [HttpGet("reference-files/{type}")]
    public Task<IActionResult> ReferenceFile(string type, CancellationToken ct)
    {
        return Send(new GetReferenceFileForConsumersQuery(type), ct);
    }

    [HttpGet("settings/{key}")]
    public Task<IActionResult> Setting(string key, CancellationToken ct)
    {
        return Send(new GetSystemSettingQuery(key), ct);
    }
}
