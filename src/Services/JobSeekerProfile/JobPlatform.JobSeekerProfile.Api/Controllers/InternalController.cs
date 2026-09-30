using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.JobSeekerProfile.Application.Queries.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.JobSeekerProfile.Api.Controllers;

/// <summary>Service-to-service reads (handover section 6.1 internal routes): client-credentials token, scope identity.internal.</summary>
[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalController : ApiControllerBase
{
    [HttpGet("profiles/{id:guid}")]
    public Task<IActionResult> GetForMatching(Guid id, CancellationToken ct)
    {
        return Send(new GetProfileForMatchingQuery(id), v => v is null ? NotFound() : Ok(v), ct);
    }

    [HttpGet("profiles/{id:guid}/privacy")]
    public Task<IActionResult> GetPrivacy(Guid id, CancellationToken ct)
    {
        return Send(new GetCandidatePrivacyQuery(id), v => v is null ? NotFound() : Ok(v), ct);
    }

    [HttpGet("profiles/{id:guid}/candidate-view")]
    public Task<IActionResult> GetCandidateView(Guid id, CancellationToken ct)
    {
        return Send(new GetCandidateViewQuery(id), v => v is null ? NotFound() : Ok(v), ct);
    }

    [HttpGet("resumes/{id:guid}/content-url")]
    public Task<IActionResult> GetResumeContentUrl(Guid id, CancellationToken ct)
    {
        return Send(new GetResumeContentUrlQuery(id), v => v is null ? NotFound() : Ok(v), ct);
    }
}
