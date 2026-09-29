using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

/// <summary>Administrator control surface over accounts (US-3.1.4-03). Requires the Administrator role with MFA satisfied.</summary>
[Route("api/v1/admin/accounts")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-AUM-FORBIDDEN")]
public sealed class AdminAccountsController : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] ActorType? actorType, [FromQuery] string? standing, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Send(new ListAccountsQuery(actorType, standing, search, page, pageSize), dto => Ok(dto), ct);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetAccountStandingQuery(id), dto => OkWithETag(dto, dto.ETag), ct);
    }

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        return SendNoContent(new ApproveUserAccountCommand(id, IfMatch), ct);
    }

    [HttpPost("{id:guid}/ban")]
    public Task<IActionResult> Ban(Guid id, ReasonRequest body, CancellationToken ct)
    {
        return SendNoContent(new BanUserAccountCommand(id, body.Reason, IfMatch), ct);
    }

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id, ReasonRequest body, CancellationToken ct)
    {
        return SendNoContent(new DeactivateUserAccountCommand(id, body.Reason, IfMatch), ct);
    }

    [HttpPost("{id:guid}/reset-credentials")]
    public Task<IActionResult> ResetCredentials(Guid id, CancellationToken ct)
    {
        return SendNoContent(new ResetCredentialsCommand(id, IfMatch), ct);
    }

    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    public Task<IActionResult> AssignRole(Guid id, Guid roleId, CancellationToken ct)
    {
        return SendNoContent(new AssignRoleToAccountCommand(id, roleId, IfMatch), ct);
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    public Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken ct)
    {
        return SendNoContent(new RemoveRoleFromAccountCommand(id, roleId, IfMatch), ct);
    }
}
