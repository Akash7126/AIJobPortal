using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Application.Authentication;

// ---------------------------------------------------------------------- refresh (sliding session, refresh-token rotation)

public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<AuthenticationResultDto>;

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("VAL.RefreshToken.Required");
}

internal sealed class RefreshSessionHandler : ICommandHandler<RefreshSessionCommand, AuthenticationResultDto>
{
    private static readonly Error Expired = Error.Unauthorized(ErrorCodes.AuthSessionExpired, "The session has expired. Sign in again.");

    private readonly ISessionStore _sessions;
    private readonly IAccountRepository _accounts;
    private readonly IApiSecretHasher _tokenHasher;
    private readonly ISecretGenerator _generator;
    private readonly Security.SessionIssuer _issuer;
    private readonly TimeProvider _clock;

    public RefreshSessionHandler(ISessionStore sessions, IAccountRepository accounts, IApiSecretHasher tokenHasher, ISecretGenerator generator,
        Security.SessionIssuer issuer, TimeProvider clock)
    {
        _sessions = sessions;
        _accounts = accounts;
        _tokenHasher = tokenHasher;
        _generator = generator;
        _issuer = issuer;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResultDto>> Handle(RefreshSessionCommand request, CancellationToken ct)
    {
        var parts = request.RefreshToken.Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var sessionId))
        {
            return Expired;
        }

        var session = await _sessions.GetAsync(sessionId, ct);
        if (session is null)
        {
            return Expired;
        }

        if (!_tokenHasher.Verify(parts[1], session.RefreshTokenHash ?? string.Empty))
        {
            // A rotated-out refresh token being replayed means it may have been stolen: kill the session.
            session.Invalidate();
            await _sessions.SaveAsync(session, ct);
            return Expired;
        }

        try
        {
            session.EnsureUsable(_clock);
        }
        catch (BusinessRuleViolationException ex)
        {
            return ex.ToError();
        }

        // INV-05: a ban or deactivation takes effect on the next authentication attempt, i.e. here.
        var account = await _accounts.GetByIdAsync(new AccountId(session.AccountId), ct);
        if (account is null || !account.CanAuthenticate(_clock.GetUtcNow().UtcDateTime))
        {
            session.Invalidate();
            await _sessions.SaveAsync(session, ct);
            return Expired;
        }

        var newSecret = _generator.GenerateToken();
        session.Touch(_clock);
        session.RotateRefreshToken(_tokenHasher.Hash(newSecret));
        await _sessions.SaveAsync(session, ct);
        return _issuer.Build(account, session, newSecret, mfaVerified: account.MfaRequired);
    }
}

// ---------------------------------------------------------------------- logout (US-3.1.5-04 AC-04)

public sealed record LogoutCommand : AuthenticatedRequest, ICommand<Unit>;

internal sealed class LogoutHandler : ICommandHandler<LogoutCommand, Unit>
{
    private readonly ICurrentUser _user;
    private readonly ISessionStore _sessions;
    private readonly IAccessTokenService _tokens;
    private readonly IAccessLog _accessLog;
    private readonly TimeProvider _clock;

    public LogoutHandler(ICurrentUser user, ISessionStore sessions, IAccessTokenService tokens, IAccessLog accessLog, TimeProvider clock)
    {
        _user = user;
        _sessions = sessions;
        _tokens = tokens;
        _accessLog = accessLog;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (_user.SessionId is { } sessionId && await _sessions.GetAsync(sessionId, ct) is { } session)
        {
            session.Invalidate();
            await _sessions.SaveAsync(session, ct);
        }

        // The access token itself stops working immediately as well, not only when it would have expired.
        if (_user.TokenId is { } tokenId)
        {
            await _sessions.RevokeTokenAsync(tokenId, _tokens.UserTokenLifetime, ct);
        }

        await _accessLog.AppendAsync(new AccessLogEntry(_clock.GetUtcNow().UtcDateTime, _user.UserId, "auth.logout", "auth", "Allow", null, _user.IpAddress), ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- change password (US-3.1.5-02)

/// <summary>A wrong current password counts as a failed attempt against the lockout, so the aggregate is persisted on failure.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : AuthenticatedRequest, ICommand<Unit>, IPersistOnFailure;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithErrorCode("VAL.CurrentPassword.Required");
        RuleFor(x => x.NewPassword).NotEmpty().WithErrorCode("VAL.NewPassword.Required")
            .MaximumLength(128).WithErrorCode("VAL.NewPassword.MaxLength");
        RuleFor(x => x.NewPassword).Must((c, n) => n != c.CurrentPassword).WithErrorCode("VAL.NewPassword.SameAsCurrent");
    }
}

internal sealed class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordPolicyRepository _policies;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionStore _sessions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ChangePasswordHandler(IAccountRepository accounts, IPasswordPolicyRepository policies, IPasswordHasher hasher, ISessionStore sessions,
        ICurrentUser user, TimeProvider clock)
    {
        _accounts = accounts;
        _policies = policies;
        _hasher = hasher;
        _sessions = sessions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(_user.UserId!.Value), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        var currentMatches = _hasher.Verify(request.CurrentPassword, account.PasswordHash.Value);
        var attempt = account.AttemptLogin(currentMatches, _clock);
        if (!attempt.IsSuccess)
        {
            return LoginErrors.From(attempt, _clock.GetUtcNow().UtcDateTime);
        }

        var policy = await _policies.GetAsync(ct);
        try
        {
            account.ChangePassword(request.NewPassword, new PasswordHash(_hasher.Hash(request.NewPassword)), policy, _clock);
        }
        catch (BusinessRuleViolationException ex)
        {
            return ex.ToError();
        }

        // Other devices must sign in again with the new password; the current session keeps working.
        await _sessions.InvalidateAllForAccountAsync(account.Id.Value, _user.SessionId, ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- e-mail verification (US-3.1.5-01 AC-02)

public sealed record VerifyEmailCommand(Guid AccountId, string Token) : ICommand<Unit>, IRateLimitedRequest
{
    public string RateLimitScope => "email-verification";
    public string RateLimitedErrorCode => ErrorCodes.AuthRateLimited;
}

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Token).NotEmpty().WithErrorCode("VAL.Token.Required").MaximumLength(256).WithErrorCode("VAL.Token.TooLong");
    }
}

internal sealed class VerifyEmailHandler : ICommandHandler<VerifyEmailCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IApiSecretHasher _tokenHasher;
    private readonly TimeProvider _clock;

    public VerifyEmailHandler(IAccountRepository accounts, IApiSecretHasher tokenHasher, TimeProvider clock)
    {
        _accounts = accounts;
        _tokenHasher = tokenHasher;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        account.VerifyEmail(request.Token, _tokenHasher, _clock);
        return Result.Success();
    }
}
