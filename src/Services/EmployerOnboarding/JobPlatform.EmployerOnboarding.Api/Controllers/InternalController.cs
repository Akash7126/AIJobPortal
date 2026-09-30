using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.EmployerOnboarding.Application.Queries.Standing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

/// <summary>Service-to-service reads for other bounded contexts (InternalService token only).</summary>
[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalController : ApiControllerBase
{
    [HttpGet("employers/{employerId:guid}/standing")]
    public Task<IActionResult> Standing(Guid employerId, CancellationToken ct)
    {
        var query = new GetEmployerStandingQuery(employerId);
        return Send(query, ct);
    }

    [HttpGet("employers/{employerId:guid}/company")]
    public Task<IActionResult> Company(Guid employerId, CancellationToken ct)
    {
        var query = new GetCompanyPublicInfoQuery(employerId);
        return Send(query, ct);
    }
}
