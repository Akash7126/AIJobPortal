using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

/// <summary>
/// Sign-in (US-3.1.5-01). Failed attempts update the lockout counters, so the aggregate is persisted even when the result is a failure.
/// The failure response is identical for an unknown user and a wrong password.
/// </summary>
/// <param name="Mechanism">password | email-verification | mfa (password + TOTP code in one call).</param>
public sealed record AuthenticateUserCommand(string Username, string? Password, string Mechanism, ActorType? ActorType, string? MfaCode, string? EmailCode)
    : ICommand<AuthenticationResultDto>, IPersistOnFailure;
