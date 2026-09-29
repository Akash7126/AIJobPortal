using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

/// <summary>Completes the sign-in of an account with a pending second factor (and confirms enrolment on first success).</summary>
public sealed record VerifyMfaCommand(string MfaToken, string Code) : ICommand<AuthenticationResultDto>, IPersistOnFailure;
