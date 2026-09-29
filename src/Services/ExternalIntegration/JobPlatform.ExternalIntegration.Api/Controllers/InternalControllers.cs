using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.ExternalIntegration.Application.Queries.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.ExternalIntegration.Api.Controllers;

/// <summary>Synchronous contract for other BCs (foundation section 9.5): BC-07 dashboards, BC-09 posting sync. Service-to-service JWT
/// only (scope identity.internal); never exposed by the gateway.</summary>
[Route("internal/v1/integrations")]
[Authorize(Policy = Policies.InternalService)]
[ForbiddenCode("E-EI-NOT-FOUND")]
public sealed class InternalIntegrationsController : ApiControllerBase
{
    [HttpGet("{sourcePlatformId:guid}")]
    public Task<IActionResult> Get(Guid sourcePlatformId, CancellationToken ct) => Send(new GetIntegrationSummaryQuery(sourcePlatformId), ct);
}
