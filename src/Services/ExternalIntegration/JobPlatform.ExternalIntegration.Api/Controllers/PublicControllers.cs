using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.ExternalIntegration.Api.Controllers;

/// <summary>US-3.4.3-03: the general API documentation is public (handover Q-02, no credential required); a deprecated version still
/// succeeds with a notice (AC-02).</summary>
[Route("api/v1")]
[AllowAnonymous]
public sealed class PublicDocumentationController : ApiControllerBase
{
    [HttpGet("docs/{version}")]
    public Task<IActionResult> Get(string version, CancellationToken ct)
    {
        return Send(new GetApiDocumentationQuery(version), value =>
        {
            if (value.Deprecated)
            {
                Response.Headers["Deprecation"] = "true";
                if (value.SunsetAtUtc is { } sunset)
                {
                    Response.Headers["Sunset"] = sunset.ToString("R");
                }
            }

            return Ok(value);
        }, ct);
    }

    /// <summary>Not in the handover's route table but needed to verify AC-03 "two versions are served concurrently" end to end.</summary>
    [HttpGet("api-versions")]
    public Task<IActionResult> List(CancellationToken ct)
    {
        return Send(new ListApiVersionsQuery(), ct);
    }
}
