using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application;
using JobPlatform.AccountIdentity.Application.Accounts;
using JobPlatform.AccountIdentity.Application.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Consent;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[Route("api/v1/api-credentials")]
public sealed class ApiCredentialsController : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.ExternalJobSite)]
    [ProducesResponseType<IssuedApiCredentialDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Issue(IssueApiCredentialRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new IssueApiCredentialCommand(body.IpWhitelist, body.MaxRequests, body.PeriodSeconds, body.ExpiresAtUtc), ct);
        Response.Headers.CacheControl = "no-store";
        return result.ToActionResult(HttpContext, dto => Created($"/api/v1/api-credentials/{dto.ApiCredentialId}", dto));
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = Policies.ExternalJobSite)]
    public Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        return SendNoContent(new RevokeApiCredentialCommand(id), ct);
    }

    [HttpGet("current")]
    [Authorize(Policy = Policies.ExternalJobSite)]
    public Task<IActionResult> Current(CancellationToken ct)
    {
        return Send(new GetCurrentApiCredentialQuery(), dto => Ok(dto), ct);
    }
}
