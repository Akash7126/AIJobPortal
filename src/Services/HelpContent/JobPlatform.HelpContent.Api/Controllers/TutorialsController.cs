using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.HelpContent.Application.Commands.Tutorials;
using JobPlatform.HelpContent.Application.Queries.Tutorials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

/// <summary>
/// US-3.7.2-07: onboarding tutorials. Deviation from the handover's literal route (proposed as "GET /tutorials/onboarding" with no id):
/// several tutorials can exist (tutorial content is HelpContent(Kind=Guide), one per audience/flow), so the tutorial id is a route
/// parameter here - see the BC-06 status doc.
/// </summary>
[Route("api/v1/tutorials")]
[Authorize(Policy = Policies.Authenticated)]
public sealed class TutorialsController : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Send(new GetOnboardingTutorialQuery(id), ct);

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct) => SendNoContent(new CompleteTutorialCommand(id), ct);
}
