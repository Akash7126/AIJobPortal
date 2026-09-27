using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application.Accounts;
using JobPlatform.AccountIdentity.Application.Administration;
using JobPlatform.AccountIdentity.Application.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Internal;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

/// <summary>RBAC administration (US-3.1.5-03). Changes apply from the next request; the cached role map is evicted immediately.</summary>
[Route("api/v1/admin/roles")]
[Authorize(Policy = Policies.Administrator)]
public sealed class AdminRolesController : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct)
    {
        return Send(new ListRolesQuery(), dto => Ok(dto), ct);
    }

    [HttpPut("{roleId:guid}/permissions/{permission}")]
    public Task<IActionResult> Grant(Guid roleId, string permission, CancellationToken ct)
    {
        return SendNoContent(new GrantPermissionCommand(roleId, permission, IfMatch), ct);
    }

    [HttpDelete("{roleId:guid}/permissions/{permission}")]
    public Task<IActionResult> Revoke(Guid roleId, string permission, CancellationToken ct)
    {
        return SendNoContent(new RevokePermissionCommand(roleId, permission, IfMatch), ct);
    }
}
