using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.CandidateSourcing.Api.Controllers;

/// <summary>Employer-facing candidate sourcing surface (Employer role). Every refusal carries E-CRFE-FORBIDDEN.</summary>
[Route("api/v1/employers/me")]
[Authorize(Policy = Policies.Employer)]
[ForbiddenCode("E-CRFE-FORBIDDEN")]
public abstract class CandidateSourcingControllerBase : ApiControllerBase
{
}
