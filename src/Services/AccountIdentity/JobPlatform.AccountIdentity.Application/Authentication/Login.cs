using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Application.Authentication;

public static class LoginMechanisms
{
    public const string Password = "password";
    public const string EmailVerification = "email-verification";
    public const string Mfa = "mfa";
}

/// <summary>
/// Sign-in (US-3.1.5-01). Failed attempts update the lockout counters, so the aggregate is persisted even when the result is a failure.
/// The failure response is identical for an unknown user and a wrong password.
/// </summary>
/// <param name="Mechanism">password | email-verification | mfa (password + TOTP code in one call).</param>
public sealed record AuthenticateUserCommand(string Username, string? Password, string Mechanism, ActorType? ActorType, string? MfaCode, string? EmailCode)
    : ICommand<AuthenticationResultDto>, IPersistOnFailure;

public sealed class AuthenticateUserValidator : AbstractValidator<AuthenticateUserCommand>
{
    private static readonly string[] Mechanisms = { LoginMechanisms.Password, LoginMechanisms.EmailVerification, LoginMechanisms.Mfa };

    public AuthenticateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithErrorCode("VAL.Username.Required").MaximumLength(254).WithErrorCode("VAL.Username.TooLong");
        RuleFor(x => x.Mechanism).Must(m => m is not null && Mechanisms.Contains(m)).WithErrorCode("VAL.Mechanism.Invalid");
        When(x => x.Mechanism is LoginMechanisms.Password or LoginMechanisms.Mfa, () =>
            RuleFor(x => x.Password).NotEmpty().WithErrorCode("VAL.Password.Required").MaximumLength(128).WithErrorCode("VAL.Password.MaxLength"));
        When(x => x.Mechanism == LoginMechanisms.Mfa, () =>
            RuleFor(x => x.MfaCode).NotEmpty().WithErrorCode("VAL.MfaCode.Required").DependentRules(() =>
                RuleFor(x => x.MfaCode!).Matches("^[0-9]{6}$").WithErrorCode("VAL.MfaCode.Format")));
        When(x => x.Mechanism == LoginMechanisms.EmailVerification, () =>
        {
            RuleFor(x => x.Username).ValidEmail();
            When(x => !string.IsNullOrEmpty(x.EmailCode), () =>
                RuleFor(x => x.EmailCode!).Matches("^[0-9]{6}$").WithErrorCode("VAL.EmailCode.Format"));
        });
    }
}

internal sealed class AuthenticateUserHandler : ICommandHandler<AuthenticateUserCommand, AuthenticationResultDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly IOtpHasher _otpHasher;
    private readonly ILoginCodeStore _loginCodes;
    private readonly IEmailVerificationSender _email;
    private readonly ISecretGenerator _generator;
    private readonly IMfaService _mfa;
    private readonly IMfaChallengeStore _challenges;
    private readonly SessionIssuer _issuer;
    private readonly IAccessLog _accessLog;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthenticateUserHandler> _logger;

    public AuthenticateUserHandler(IAccountRepository accounts, IPasswordHasher hasher, IOtpHasher otpHasher, ILoginCodeStore loginCodes,
        IEmailVerificationSender email, ISecretGenerator generator, IMfaService mfa, IMfaChallengeStore challenges, SessionIssuer issuer,
        IAccessLog accessLog, ICurrentUser user, TimeProvider clock, ILogger<AuthenticateUserHandler> logger)
    {
        _accounts = accounts;
        _hasher = hasher;
        _otpHasher = otpHasher;
        _loginCodes = loginCodes;
        _email = email;
        _generator = generator;
        _mfa = mfa;
        _challenges = challenges;
        _issuer = issuer;
        _accessLog = accessLog;
        _user = user;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AuthenticationResultDto>> Handle(AuthenticateUserCommand request, CancellationToken ct)
    {
        var candidates = await _accounts.FindByLoginAsync(request.Username, request.ActorType, ct);
        return request.Mechanism == LoginMechanisms.EmailVerification
            ? await HandleEmailCodeAsync(request, candidates, ct)
            : await HandlePasswordAsync(request, candidates, ct);
    }

    private async Task<Result<AuthenticationResultDto>> HandlePasswordAsync(AuthenticateUserCommand request, IReadOnlyList<Account> candidates,
        CancellationToken ct)
    {
        var password = request.Password!;
        if (candidates.Count == 0)
        {
            _hasher.SimulateVerify(password);
            await LogAsync(null, "Deny", "unknown-account", ct);
            return LoginErrors.InvalidCredentials;
        }

        var matched = candidates.FirstOrDefault(c => _hasher.Verify(password, c.PasswordHash.Value));
        if (matched is null)
        {
            var results = candidates.Select(c => (Account: c, Result: c.AttemptLogin(false, _clock))).ToList();
            await LogAsync(candidates.Count == 1 ? candidates[0].Id.Value : null, "Deny", results[0].Result.RuleCode ?? "invalid-credentials", ct);
            return LoginErrors.From(results[0].Result, Now());
        }

        var attempt = matched.AttemptLogin(true, _clock);
        if (attempt.IsSuccess && _hasher.NeedsRehash(matched.PasswordHash.Value))
        {
            // The plaintext is in hand only now: upgrade weaker hash parameters transparently (persisted with the aggregate).
            matched.UpgradePasswordHash(new PasswordHash(_hasher.Hash(password)));
        }

        return await CompleteAsync(matched, attempt, request, ct);
    }

    private async Task<Result<AuthenticationResultDto>> HandleEmailCodeAsync(AuthenticateUserCommand request, IReadOnlyList<Account> candidates,
        CancellationToken ct)
    {
        var target = candidates.FirstOrDefault(c => c.Email is not null && c.EmailVerifiedAtUtc is not null && c.Standing == AccountStanding.Active);
        if (string.IsNullOrEmpty(request.EmailCode))
        {
            // Step 1: always answer the same way so the endpoint cannot be used to discover which e-mails are registered.
            if (target is not null)
            {
                var code = _generator.GenerateOtp();
                await _loginCodes.StoreAsync(target.Id.Value, _otpHasher.Hash(code), TimeSpan.FromMinutes(10), ct);
                try
                {
                    await _email.SendLoginCodeAsync(target.Email!, code, _user.Language, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not deliver a login code for account {AccountId}", target.Id.Value);
                }
            }

            return new AuthenticationResultDto(AuthenticationStatus.EmailCodeSent, null, null, null, false);
        }

        if (target is null)
        {
            await LogAsync(null, "Deny", "unknown-account", ct);
            return LoginErrors.InvalidCredentials;
        }

        var valid = await _loginCodes.VerifyAndConsumeAsync(target.Id.Value, request.EmailCode, _otpHasher, 5, ct);
        return await CompleteAsync(target, target.AttemptLogin(valid, _clock), request, ct);
    }

    private async Task<Result<AuthenticationResultDto>> CompleteAsync(Account account, LoginAttemptResult attempt, AuthenticateUserCommand request,
        CancellationToken ct)
    {
        if (!attempt.IsSuccess)
        {
            await LogAsync(account.Id.Value, "Deny", attempt.RuleCode ?? "invalid-credentials", ct);
            return LoginErrors.From(attempt, Now());
        }

        if (!account.MfaRequired)
        {
            await LogAsync(account.Id.Value, "Allow", null, ct);
            return await _issuer.IssueAsync(account, mfaVerified: false, ct);
        }

        if (request.Mechanism == LoginMechanisms.Mfa && !string.IsNullOrEmpty(request.MfaCode) && account.MfaEnabled && account.MfaSecret is not null)
        {
            var matchedStep = _mfa.FindMatchingTimeStep(_mfa.Unprotect(account.MfaSecret), request.MfaCode, Now());
            var mfaAttempt = account.AttemptMfa(matchedStep, _clock);
            if (!mfaAttempt.IsSuccess)
            {
                await LogAsync(account.Id.Value, "Deny", mfaAttempt.RuleCode ?? "invalid-mfa-code", ct);
                return LoginErrors.From(mfaAttempt, Now());
            }

            await LogAsync(account.Id.Value, "Allow", null, ct);
            return await _issuer.IssueAsync(account, mfaVerified: true, ct);
        }

        var challenge = await _challenges.CreateAsync(account.Id.Value, ct);
        await LogAsync(account.Id.Value, "Pending", "mfa-required", ct);
        return new AuthenticationResultDto(account.MfaEnabled ? AuthenticationStatus.MfaRequired : AuthenticationStatus.MfaEnrollmentRequired,
            null, challenge.Token, challenge.ExpiresAtUtc, account.MustChangePassword);
    }

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private Task LogAsync(Guid? accountId, string decision, string? reason, CancellationToken ct) =>
        _accessLog.AppendAsync(new AccessLogEntry(Now(), accountId, "auth.login", "auth", decision, reason, _user.IpAddress), ct);
}
