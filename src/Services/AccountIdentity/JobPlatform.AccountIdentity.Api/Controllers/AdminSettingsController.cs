using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Application.Queries.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

/// <summary>Security settings: password policy (US-3.1.5-02) and session timeout (US-3.1.5-04).</summary>
[Route("api/v1/admin")]
[Authorize(Policy = Policies.Administrator)]
public sealed class AdminSettingsController : ApiControllerBase
{
    [HttpGet("password-policy")]
    public Task<IActionResult> GetPasswordPolicy(CancellationToken ct)
    {
        var query = new GetPasswordPolicyQuery();
        return Send(query, dto => OkWithETag(dto, dto.ETag), ct);
    }

    [HttpPut("password-policy")]
    public Task<IActionResult> ConfigurePasswordPolicy(ConfigurePasswordPolicyRequest body, CancellationToken ct)
    {
        var command = new ConfigurePasswordPolicyCommand(body.MinLength, body.RequireUpper, body.RequireLower, body.RequireDigit, IfMatch);
        return SendNoContent(command, ct);
    }

    [HttpGet("session-timeout")]
    public Task<IActionResult> GetSessionTimeout(CancellationToken ct)
    {
        var query = new GetSessionTimeoutQuery();
        return Send(query, dto => OkWithETag(dto, dto.ETag), ct);
    }

    [HttpPut("session-timeout")]
    public Task<IActionResult> ConfigureSessionTimeout(ConfigureSessionTimeoutRequest body, CancellationToken ct)
    {
        var command = new ConfigureSessionTimeoutCommand(body.IdleTimeoutMinutes, IfMatch);
        return SendNoContent(command, ct);
    }
}
