using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Authentication;

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
