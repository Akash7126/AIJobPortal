using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Sessions;

public enum SessionStatus
{
    Active,
    Invalidated
}

public static class SessionRuleCodes
{
    public const string Expired = "AI.UserSession.EXPIRED";
    public const string Invalidated = "AI.UserSession.INVALIDATED";
    public const string AdminOnly = "AI.SessionTimeout.ADMIN_ONLY";
    public const string InvalidTimeout = "AI.SessionTimeout.INVALID_TIMEOUT";
}

/// <summary>Serializable state of a session (kept in Redis, not in SQL).</summary>
public sealed record SessionSnapshot(
    Guid SessionId,
    Guid AccountId,
    DateTime CreatedAtUtc,
    DateTime LastActivityUtc,
    int IdleTimeoutMinutes,
    SessionStatus Status,
    string? RefreshTokenHash);

/// <summary>
/// Authenticated period of one account. The idle timeout is captured at creation, so an administrator changing
/// the setting affects only sessions created afterwards (US-3.1.5-04 AC-03). Touching resets the idle timer.
/// </summary>
public sealed class UserSession
{
    private UserSession()
    {
    }

    public Guid SessionId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityUtc { get; private set; }
    public int IdleTimeoutMinutes { get; private set; }
    public SessionStatus Status { get; private set; }
    public string? RefreshTokenHash { get; private set; }

    public static UserSession Create(Guid accountId, int idleTimeoutMinutes, string? refreshTokenHash, TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new UserSession
        {
            SessionId = Guid.NewGuid(),
            AccountId = accountId,
            CreatedAtUtc = now,
            LastActivityUtc = now,
            IdleTimeoutMinutes = idleTimeoutMinutes,
            Status = SessionStatus.Active,
            RefreshTokenHash = refreshTokenHash
        };
    }

    public static UserSession Rehydrate(SessionSnapshot s) => new()
    {
        SessionId = s.SessionId,
        AccountId = s.AccountId,
        CreatedAtUtc = s.CreatedAtUtc,
        LastActivityUtc = s.LastActivityUtc,
        IdleTimeoutMinutes = s.IdleTimeoutMinutes,
        Status = s.Status,
        RefreshTokenHash = s.RefreshTokenHash
    };

    public SessionSnapshot ToSnapshot() =>
        new(SessionId, AccountId, CreatedAtUtc, LastActivityUtc, IdleTimeoutMinutes, Status, RefreshTokenHash);

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(IdleTimeoutMinutes);

    /// <summary>Expired once the idle period reaches the captured timeout.</summary>
    public bool IsExpired(TimeProvider clock) => clock.GetUtcNow().UtcDateTime - LastActivityUtc >= IdleTimeout;

    public bool IsUsable(TimeProvider clock) => Status == SessionStatus.Active && !IsExpired(clock);

    /// <summary>Records activity, resetting the idle timer. Throws when the session is no longer usable.</summary>
    public void Touch(TimeProvider clock)
    {
        EnsureUsable(clock);
        LastActivityUtc = clock.GetUtcNow().UtcDateTime;
    }

    public void EnsureUsable(TimeProvider clock)
    {
        Guard.Ensure(Status == SessionStatus.Active, SessionRuleCodes.Invalidated, "The session has been invalidated.",
            ErrorCodes.AuthSessionExpired, BusinessRuleKind.Unauthorized);
        Guard.Ensure(!IsExpired(clock), SessionRuleCodes.Expired, "The session has expired.",
            ErrorCodes.AuthSessionExpired, BusinessRuleKind.Unauthorized);
    }

    public void RotateRefreshToken(string newRefreshTokenHash) => RefreshTokenHash = newRefreshTokenHash;

    /// <summary>Logout, ban or credential reset: the session token stops working immediately (US-3.1.5-04 AC-04).</summary>
    public void Invalidate() => Status = SessionStatus.Invalidated;
}

/// <summary>Administrator-configurable idle timeout (singleton, versioned).</summary>
public sealed class SessionTimeoutSetting : AggregateRoot<Guid>
{
    public static readonly Guid SingletonId = new("0f1b6a3e-0000-4000-8000-000000000002");

    private SessionTimeoutSetting()
    {
    }

    public int IdleTimeoutMinutes { get; private set; }
    public int SettingVersion { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static SessionTimeoutSetting CreateDefault(TimeProvider clock) => new()
    {
        Id = SingletonId,
        IdleTimeoutMinutes = AccountDefaults.DefaultIdleTimeoutMinutes,
        SettingVersion = 1,
        UpdatedAtUtc = clock.GetUtcNow().UtcDateTime
    };

    public void Configure(Actor admin, int idleTimeoutMinutes, TimeProvider clock)
    {
        Guard.Ensure(admin.IsAdministrator, SessionRuleCodes.AdminOnly, "Administrator role required.", ErrorCodes.AuthForbidden,
            BusinessRuleKind.Forbidden);
        Guard.Ensure(idleTimeoutMinutes is >= AccountDefaults.MinIdleTimeoutMinutes and <= AccountDefaults.MaxIdleTimeoutMinutes,
            SessionRuleCodes.InvalidTimeout, "The idle timeout must be between 5 and 480 minutes.", ErrorCodes.AuthInvalidField,
            BusinessRuleKind.InvalidInput);

        IdleTimeoutMinutes = idleTimeoutMinutes;
        SettingVersion++;
        UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        UpdatedBy = admin.Id;
    }
}
