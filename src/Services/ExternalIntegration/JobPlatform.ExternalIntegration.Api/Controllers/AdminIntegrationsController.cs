using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.ExternalIntegration.Api.Controllers;

/// <summary>US-3.4.1-01: MoL/PEF admission of a non-recommended site, then activation/suspension. Every refusal carries E-EJSI-FORBIDDEN.</summary>
[Route("api/v1/admin/integrations")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-EJSI-FORBIDDEN")]
public sealed class AdminIntegrationsController : ApiControllerBase
{
    public sealed record ApproveRequest(string ApprovalBasis);

    public sealed record SuspendRequest(string Reason);

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest body, CancellationToken ct)
    {
        var command = new ApproveExternalJobSiteCommand(id, body.ApprovalBasis);
        return SendNoContent(command, ct);
    }

    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var command = new ActivateIntegrationCommand(id);
        return SendNoContent(command, ct);
    }

    [HttpPost("{id:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid id, [FromBody] SuspendRequest body, CancellationToken ct)
    {
        var command = new SuspendIntegrationCommand(id, body.Reason);
        return SendNoContent(command, ct);
    }
}
