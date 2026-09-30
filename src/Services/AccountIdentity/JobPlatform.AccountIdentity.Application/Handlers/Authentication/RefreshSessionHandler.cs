using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.DTOs.Authentication;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

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
