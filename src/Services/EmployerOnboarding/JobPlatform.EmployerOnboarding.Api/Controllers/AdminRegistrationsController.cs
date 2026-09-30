using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.EmployerOnboarding.Application.Commands.Registration;
using JobPlatform.EmployerOnboarding.Application.Queries.Registration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

/// <summary>US-3.1.4-04: administrator review of employer admission. Every refusal carries E-AUM-FORBIDDEN.</summary>
[Route("api/v1/admin/employer-registrations")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-AUM-FORBIDDEN")]
public sealed class AdminRegistrationsController : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListEmployerRegistrationsQuery(status, page, pageSize);
        return Send(query, ct);
    }

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var command = new ApproveEmployerRegistrationCommand(id);
        return SendNoContent(command, ct);
    }
}
