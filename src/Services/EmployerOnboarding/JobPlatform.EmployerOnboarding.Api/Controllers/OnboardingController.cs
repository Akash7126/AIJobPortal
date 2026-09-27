using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

[Route("api/v1/employers/onboarding")]
[Authorize]
public sealed class OnboardingController : ApiControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start([FromBody] StartOnboardingRequest body, CancellationToken ct)
    {
        return Send(new StartOnboardingProcessCommand(body.EmployerId), dto => Created($"/api/v1/employers/onboarding/{dto.ProcessId}", dto), ct);
    }

    [HttpGet("{processId:guid}")]
    public Task<IActionResult> Get(Guid processId, CancellationToken ct)
    {
        return Send(new GetOnboardingProcessQuery(processId), ct);
    }
}
