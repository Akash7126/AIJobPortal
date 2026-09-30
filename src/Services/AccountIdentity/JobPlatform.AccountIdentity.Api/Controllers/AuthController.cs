using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResultDto>(StatusCodes.Status200OK)]
    public Task<IActionResult> Login(LoginRequest body, CancellationToken ct)
    {
        var command = new AuthenticateUserCommand(body.Username, body.Password, body.Mechanism, body.ActorType, body.MfaCode, body.EmailCode);
        return Send(command,
            dto => Ok(dto), ct);
    }

    [HttpPost("mfa/enroll")]
    [AllowAnonymous]
    public Task<IActionResult> EnrollMfa(MfaEnrollRequest body, CancellationToken ct)
    {
        var command = new BeginMfaEnrollmentCommand(body.MfaToken);
        return Send(command, dto => Ok(dto), ct);
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public Task<IActionResult> VerifyMfa(MfaVerifyRequest body, CancellationToken ct)
    {
        var command = new VerifyMfaCommand(body.MfaToken, body.Code);
        return Send(command, dto => Ok(dto), ct);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public Task<IActionResult> Refresh(RefreshRequest body, CancellationToken ct)
    {
        var command = new RefreshSessionCommand(body.RefreshToken);
        return Send(command, dto => Ok(dto), ct);
    }

    [HttpPost("logout")]
    [Authorize(Policy = Policies.AuthenticatedAllowingPasswordChange)]
    public Task<IActionResult> Logout(CancellationToken ct)
    {
        var command = new LogoutCommand();
        return SendNoContent(command, ct);
    }

    [HttpPost("email-verification")]
    [AllowAnonymous]
    public Task<IActionResult> VerifyEmail(EmailVerificationRequest body, CancellationToken ct)
    {
        var command = new VerifyEmailCommand(body.AccountId, body.Token);
        return SendNoContent(command, ct);
    }

    [HttpPost("password")]
    [Authorize(Policy = Policies.AuthenticatedAllowingPasswordChange)]
    public Task<IActionResult> ChangePassword(ChangePasswordRequest body, CancellationToken ct)
    {
        var command = new ChangePasswordCommand(body.CurrentPassword, body.NewPassword);
        return SendNoContent(command, ct);
    }
}
