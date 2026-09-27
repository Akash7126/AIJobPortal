using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

[Route("api/v1/employers/onboarding/{processId:guid}/feedback")]
[Authorize]
public sealed class FeedbackController : ApiControllerBase
{
    public sealed record FeedbackRequest(string Comments, int Rating);

    [HttpPost]
    public Task<IActionResult> Submit(Guid processId, [FromBody] FeedbackRequest body, CancellationToken ct)
    {
        return SendNoContent(new SubmitOnboardingFeedbackCommand(processId, body.Comments, body.Rating), ct);
    }
}
