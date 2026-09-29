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
        return Send(new AuthenticateUserCommand(body.Username, body.Password, body.Mechanism, body.ActorType, body.MfaCode, body.EmailCode),
            dto => Ok(dto), ct);
    }

    [HttpPost("mfa/enroll")]
    [AllowAnonymous]
    public Task<IActionResult> EnrollMfa(MfaEnrollRequest body, CancellationToken ct)
    {
        return Send(new BeginMfaEnrollmentCommand(body.MfaToken), dto => Ok(dto), ct);
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public Task<IActionResult> VerifyMfa(MfaVerifyRequest body, CancellationToken ct)
    {
        return Send(new VerifyMfaCommand(body.MfaToken, body.Code), dto => Ok(dto), ct);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public Task<IActionResult> Refresh(RefreshRequest body, CancellationToken ct)
    {
        return Send(new RefreshSessionCommand(body.RefreshToken), dto => Ok(dto), ct);
    }

    [HttpPost("logout")]
    [Authorize(Policy = Policies.AuthenticatedAllowingPasswordChange)]
    public Task<IActionResult> Logout(CancellationToken ct)
    {
        return SendNoContent(new LogoutCommand(), ct);
    }

    [HttpPost("email-verification")]
    [AllowAnonymous]
    public Task<IActionResult> VerifyEmail(EmailVerificationRequest body, CancellationToken ct)
    {
        return SendNoContent(new VerifyEmailCommand(body.AccountId, body.Token), ct);
    }

    [HttpPost("password")]
    [Authorize(Policy = Policies.AuthenticatedAllowingPasswordChange)]
    public Task<IActionResult> ChangePassword(ChangePasswordRequest body, CancellationToken ct)
    {
        return SendNoContent(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);
    }
}
