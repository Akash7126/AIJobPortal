using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Application.Authentication;

/// <summary>Step 1 of MFA enrolment: returns the TOTP seed once. Requires the challenge token from the password step.</summary>
public sealed record BeginMfaEnrollmentCommand(string MfaToken) : ICommand<MfaEnrollmentDto>;

public sealed record MfaEnrollmentDto(string Secret, string ProvisioningUri);

/// <summary>Completes the sign-in of an account with a pending second factor (and confirms enrolment on first success).</summary>
public sealed record VerifyMfaCommand(string MfaToken, string Code) : ICommand<AuthenticationResultDto>, IPersistOnFailure;

public sealed class BeginMfaEnrollmentValidator : AbstractValidator<BeginMfaEnrollmentCommand>
{
    public BeginMfaEnrollmentValidator() => RuleFor(x => x.MfaToken).NotEmpty().WithErrorCode("VAL.MfaToken.Required");
}

public sealed class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.MfaToken).NotEmpty().WithErrorCode("VAL.MfaToken.Required");
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.MfaCode.Required").DependentRules(() =>
            RuleFor(x => x.Code).Matches("^[0-9]{6}$").WithErrorCode("VAL.MfaCode.Format"));
    }
}

internal sealed class BeginMfaEnrollmentHandler : ICommandHandler<BeginMfaEnrollmentCommand, MfaEnrollmentDto>
{
    private readonly IMfaChallengeStore _challenges;
    private readonly IAccountRepository _accounts;
    private readonly IMfaService _mfa;

    public BeginMfaEnrollmentHandler(IMfaChallengeStore challenges, IAccountRepository accounts, IMfaService mfa)
    {
        _challenges = challenges;
        _accounts = accounts;
        _mfa = mfa;
    }

    public async Task<Result<MfaEnrollmentDto>> Handle(BeginMfaEnrollmentCommand request, CancellationToken ct)
    {
        var accountId = await _challenges.GetAccountIdAsync(request.MfaToken, ct);
        var account = accountId is null ? null : await _accounts.GetByIdAsync(new AccountId(accountId.Value), ct);
        if (account is null)
        {
            return LoginErrors.InvalidCredentials;
        }

        var secret = _mfa.GenerateSecret();
        account.BeginMfaEnrollment(_mfa.Protect(secret));
        var label = account.Email?.Value ?? account.Mobile.Value;
        return new MfaEnrollmentDto(secret, _mfa.BuildProvisioningUri("JobPlatform", label, secret));
    }
}

internal sealed class VerifyMfaHandler : ICommandHandler<VerifyMfaCommand, AuthenticationResultDto>
{
    private readonly IMfaChallengeStore _challenges;
    private readonly IAccountRepository _accounts;
    private readonly IMfaService _mfa;
    private readonly SessionIssuer _issuer;
    private readonly IAccessLog _accessLog;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public VerifyMfaHandler(IMfaChallengeStore challenges, IAccountRepository accounts, IMfaService mfa, SessionIssuer issuer, IAccessLog accessLog,
        ICurrentUser user, TimeProvider clock)
    {
        _challenges = challenges;
        _accounts = accounts;
        _mfa = mfa;
        _issuer = issuer;
        _accessLog = accessLog;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResultDto>> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        var accountId = await _challenges.GetAccountIdAsync(request.MfaToken, ct);
        var account = accountId is null ? null : await _accounts.GetByIdAsync(new AccountId(accountId.Value), ct);
        if (account is null)
        {
            return LoginErrors.InvalidCredentials;
        }

        if (account.MfaSecret is null)
        {
            return new BusinessRuleViolationException(Domain.Accounts.AccountRuleCodes.MfaNotStarted, "Multi-factor enrolment has not been started.",
                ErrorCodes.AdminStateConflict, BusinessRuleKind.Conflict).ToError();
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var matchedStep = _mfa.FindMatchingTimeStep(_mfa.Unprotect(account.MfaSecret), request.Code, now);
        var attempt = account.AttemptMfa(matchedStep, _clock);
        if (!attempt.IsSuccess)
        {
            await _accessLog.AppendAsync(new AccessLogEntry(now, account.Id.Value, "auth.mfa", "auth", "Deny", attempt.RuleCode ?? "invalid-mfa-code", _user.IpAddress), ct);
            return LoginErrors.From(attempt, now);
        }

        account.ConfirmMfaEnrollment(_clock);
        await _challenges.DeleteAsync(request.MfaToken, ct);
        await _accessLog.AppendAsync(new AccessLogEntry(now, account.Id.Value, "auth.mfa", "auth", "Allow", null, _user.IpAddress), ct);
        return await _issuer.IssueAsync(account, mfaVerified: true, ct);
    }
}
