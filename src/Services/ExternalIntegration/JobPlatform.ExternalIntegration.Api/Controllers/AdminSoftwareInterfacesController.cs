using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.ExternalIntegration.Api.Controllers;

/// <summary>US-4.3-01: the software-interface registry (external job sites, government DBs, email/SMS gateways, analytics tools).</summary>
[Route("api/v1/admin/software-interfaces")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-SI-FORBIDDEN")]
public sealed class AdminSoftwareInterfacesController : ApiControllerBase
{
    public sealed record RegisterRequest(string Category, string Name, string Endpoint);

    [HttpPut]
    public Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct) =>
        Send(new RegisterSoftwareInterfaceCommand(body.Category, body.Name, body.Endpoint), ct);
}
