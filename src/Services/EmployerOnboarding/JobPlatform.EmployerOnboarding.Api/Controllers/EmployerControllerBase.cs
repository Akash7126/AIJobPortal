using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

/// <summary>Employer self-service surface (Employer role). Every refusal carries E-ERPM-FORBIDDEN.</summary>
[Route("api/v1/employers/me")]
[Authorize(Policy = Policies.Employer)]
[ForbiddenCode("E-ERPM-FORBIDDEN")]
public abstract class EmployerControllerBase : ApiControllerBase
{
}
