using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[Route("api/v1/api-credentials")]
public sealed class ApiCredentialsController : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.ExternalJobSite)]
    [ProducesResponseType<IssuedApiCredentialDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Issue(IssueApiCredentialRequest body, CancellationToken ct)
    {
        var command = new IssueApiCredentialCommand(body.IpWhitelist, body.MaxRequests, body.PeriodSeconds, body.ExpiresAtUtc);
        var result = await Sender.Send(command, ct);
        Response.Headers.CacheControl = "no-store";
        return result.ToActionResult(HttpContext, dto => Created($"/api/v1/api-credentials/{dto.ApiCredentialId}", dto));
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = Policies.ExternalJobSite)]
    public Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var command = new RevokeApiCredentialCommand(id);
        return SendNoContent(command, ct);
    }

    [HttpGet("current")]
    [Authorize(Policy = Policies.ExternalJobSite)]
    public Task<IActionResult> Current(CancellationToken ct)
    {
        var query = new GetCurrentApiCredentialQuery();
        return Send(query, dto => Ok(dto), ct);
    }
}
