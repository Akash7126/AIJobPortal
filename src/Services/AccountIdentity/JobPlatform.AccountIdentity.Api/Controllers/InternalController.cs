using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

/// <summary>Service-to-service endpoints (not exposed by the gateway). Require a client-credentials token with the internal scope.</summary>
[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalController : ApiControllerBase
{
    [HttpGet("accounts/{id:guid}")]
    public Task<IActionResult> GetAccount(Guid id, CancellationToken ct)
    {
        var query = new GetAccountSummaryQuery(id);
        return Send(query, dto => Ok(dto), ct);
    }

    [HttpGet("api-credentials/{id:guid}/controls")]
    public Task<IActionResult> GetCredentialControls(Guid id, CancellationToken ct)
    {
        var query = new GetApiCredentialControlsQuery(id);
        return Send(query, dto => Ok(dto), ct);
    }

    [HttpGet("access-log")]
    public Task<IActionResult> ListAccessLog([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? accountId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = new ListAccessLogQuery(from, to, accountId, page, pageSize);
        return Send(query, dto => Ok(dto), ct);
    }

    [HttpPost("accounts/{id:guid}/deactivation-requests")]
    public Task<IActionResult> RequestDeactivation(Guid id, DeactivationRequest body, CancellationToken ct)
    {
        var command = new RequestAccountDeactivationCommand(id, body.Kind, body.Reason);
        return Send(command, dto => Accepted(dto), ct);
    }

    [HttpPost("accounts/{id:guid}/check-permission")]
    public Task<IActionResult> CheckPermission(Guid id, [FromBody] PermissionCheckBody body, CancellationToken ct)
    {
        var query = new CheckPermissionQuery(id, body.Permission);
        return Send(query, dto => Ok(dto), ct);
    }

    public sealed record PermissionCheckBody(string Permission);
}
