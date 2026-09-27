using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.JobSeekerProfile.Application.ShareLink;
using JobPlatform.JobSeekerProfile.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.JobSeekerProfile.Api.Controllers;

/// <summary>US-3.1.1-08. JobSeeker role.</summary>
[Route("api/v1/profiles/me/share-link")]
[Authorize(Policy = Policies.JobSeeker)]
[ForbiddenCode(ErrorCodes.Forbidden)]
public sealed class ShareLinkController : ApiControllerBase
{
    [HttpPost]
    public Task<IActionResult> Create(CancellationToken ct) => Send(new CreateProfileShareLinkCommand(), ct);

    [HttpGet]
    public Task<IActionResult> GetMine(CancellationToken ct) => Send(new GetMyShareLinkQuery(), v => v is null ? NotFound() : Ok(v), ct);
}

/// <summary>US-3.1.1-08 AC-04: anonymous read of a shared profile by token - not behind any role policy.</summary>
[Route("shared")]
[AllowAnonymous]
public sealed class SharedProfileController : ApiControllerBase
{
    [HttpGet("{token}")]
    public Task<IActionResult> Get(string token, CancellationToken ct) => Send(new GetSharedProfileQuery(token), ct);

    [HttpGet("{token}/qr")]
    public async Task<IActionResult> GetQr(string token, CancellationToken ct)
    {
        var result = await Sender.Send(new GetSharedProfileQrQuery(token), ct);
        return result.ToActionResult(HttpContext, svg => Content(svg, "image/svg+xml"));
    }
}
