using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.Queries.EmployerVerifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.GovernmentIntegration.Api.Controllers;

/// <summary>US-3.1.2-03: employer self-service submission, and its administrator (MoL reviewer) decision surface (handover section 6.1).</summary>
[Route("api/v1/employer-verifications")]
[ForbiddenCode("E-GI-FORBIDDEN")]
public sealed class EmployerVerificationsController : ApiControllerBase
{
    public sealed record RequestEmployerVerificationRequest(string RegistrationNumber, string VatNumber, string MobileNumber);

    public sealed record DecideRequest(ManualDecision Decision, string? Reason);

    [HttpPost]
    [Authorize(Policy = Policies.Employer)]
    public Task<IActionResult> Submit([FromBody] RequestEmployerVerificationRequest body, CancellationToken ct)
    {
        return Send(new RequestEmployerVerificationCommand(body.RegistrationNumber, body.VatNumber, body.MobileNumber, IdempotencyKey),
            value => Accepted($"/api/v1/employer-verifications/{value.EmployerVerificationId}", value), ct);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Authenticated)]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetEmployerVerificationQuery(id), ct);
    }

    [HttpGet("pending-review")]
    [Authorize(Policy = Policies.Administrator)]
    public Task<IActionResult> ListPendingReview([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListPendingManualReviewQuery(page, pageSize), ct);
    }

    [HttpPost("{id:guid}/manual-decision")]
    [Authorize(Policy = Policies.Administrator)]
    public Task<IActionResult> DecideManually(Guid id, [FromBody] DecideRequest body, CancellationToken ct)
    {
        return SendNoContent(new DecideEmployerVerificationManuallyCommand(id, body.Decision, body.Reason), ct);
    }
}
