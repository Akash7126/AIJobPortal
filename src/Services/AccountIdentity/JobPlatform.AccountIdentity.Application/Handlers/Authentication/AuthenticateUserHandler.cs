using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Security;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

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
