using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

/// <summary>Step 1 of MFA enrolment: returns the TOTP seed once. Requires the challenge token from the password step.</summary>
public sealed record BeginMfaEnrollmentCommand(string MfaToken) : ICommand<MfaEnrollmentDto>;
