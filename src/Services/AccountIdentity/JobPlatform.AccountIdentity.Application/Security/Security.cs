using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Security;

/// <summary>Creates a session (timeout captured at creation, refresh token stored hashed) and the access token that references it.</summary>
internal sealed class SessionIssuer
{
    private readonly ISessionTimeoutSettingRepository _timeouts;
    private readonly ISessionStore _sessions;
    private readonly IAccessTokenService _tokens;
    private readonly IApiSecretHasher _tokenHasher;
    private readonly ISecretGenerator _generator;
    private readonly TimeProvider _clock;

    public SessionIssuer(ISessionTimeoutSettingRepository timeouts, ISessionStore sessions, IAccessTokenService tokens, IApiSecretHasher tokenHasher,
        ISecretGenerator generator, TimeProvider clock)
    {
        _timeouts = timeouts;
        _sessions = sessions;
        _tokens = tokens;
        _tokenHasher = tokenHasher;
        _generator = generator;
        _clock = clock;
    }

    public async Task<AuthenticationResultDto> IssueAsync(Account account, bool mfaVerified, CancellationToken ct)
    {
        account.EnsureMfaSatisfied(mfaVerified);
        var setting = await _timeouts.GetAsync(ct);
        var refreshSecret = _generator.GenerateToken();
        var session = UserSession.Create(account.Id.Value, setting.IdleTimeoutMinutes, _tokenHasher.Hash(refreshSecret), _clock);
        await _sessions.CreateAsync(session, ct);
        return Build(account, session, refreshSecret, mfaVerified);
    }

    public AuthenticationResultDto Build(Account account, UserSession session, string refreshSecret, bool mfaVerified)
    {
        var roleIds = account.RoleAssignments.Select(r => r.RoleId.Value).ToArray();
        var token = _tokens.IssueUserToken(new UserTokenRequest(account.Id.Value, account.ActorType, roleIds, session.SessionId, mfaVerified,
            account.MustChangePassword));
        var pair = new TokenPairDto(token.Token, $"{session.SessionId:N}.{refreshSecret}", "Bearer",
            (int)Math.Max(0, (token.ExpiresAtUtc - _clock.GetUtcNow().UtcDateTime).TotalSeconds), token.ExpiresAtUtc);
        return new AuthenticationResultDto(AuthenticationStatus.Authenticated, pair, null, null, account.MustChangePassword);
    }
}

/// <summary>
/// RBAC decision point (US-3.1.5-03). Roles are resolved per request from the cached role map, so a role change applies on the next
/// request without re-login (AC-04); every decision is appended to the access log (AC-03).
/// </summary>
internal sealed class AccessAuthorizer : IAccessAuthorizer
{
    private readonly IRoleDirectory _roles;
    private readonly IAccessLog _accessLog;
    private readonly TimeProvider _clock;

    public AccessAuthorizer(IRoleDirectory roles, IAccessLog accessLog, TimeProvider clock)
    {
        _roles = roles;
        _accessLog = accessLog;
        _clock = clock;
    }

    public async Task<Result<Unit>> AuthorizeAsync(ICurrentUser user, IAuthorizedRequest request, string requestName, CancellationToken ct = default)
    {
        // Service (client-credentials) tokens have no account id; every other actor must.
        if (!user.IsAuthenticated || user.ActorType is null || (user.ActorType != ActorType.System && user.UserId is null))
        {
            return Error.Unauthorized(ErrorCodes.AuthUnauthorized, "Authentication is required.");
        }

        var deny = await EvaluateAsync(user, request, ct);
        await _accessLog.AppendAsync(new AccessLogEntry(_clock.GetUtcNow().UtcDateTime, user.UserId, requestName, request.RequiredPermission ?? requestName,
            deny is null ? "Allow" : "Deny", deny?.Reason, user.IpAddress), ct);

        return deny is null
            ? Result.Success()
            : Error.Forbidden(request.ForbiddenErrorCode, "You are not allowed to perform this action.");
    }

    private async Task<(string Reason, bool Denied)?> EvaluateAsync(ICurrentUser user, IAuthorizedRequest request, CancellationToken ct)
    {
        var allowed = request.AllowedActorTypes;
        if (allowed.Count > 0 && !allowed.Contains(user.ActorType!.Value))
        {
            return ("actor-type-not-allowed", true);
        }

        if (request.RequireMfa && !user.MfaVerified)
        {
            return ("mfa-required", true);
        }

        if (user.ActorType == ActorType.System)
        {
            // Service tokens are scope-based; they hold no roles.
            return user.Scopes.Contains(Scopes.Internal) ? null : ("missing-internal-scope", true);
        }

        if (request.RequiredPermission is { } permission)
        {
            var roles = await _roles.GetRolesForAccountAsync(user.UserId!.Value, ct);
            var decision = AccessPolicy.Authorise(roles, permission);
            if (!decision.Allowed)
            {
                return (decision.Reason ?? "permission-denied", true);
            }
        }

        return null;
    }
}
