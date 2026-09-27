using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.SharedKernel.Application.Ports;

/// <summary>The authenticated caller, as seen by the application layer. Implemented by the Api from the validated token.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    ActorType? ActorType { get; }
    IReadOnlyCollection<Guid> RoleIds { get; }
    Guid? SessionId { get; }
    bool MfaVerified { get; }
    bool MustChangePassword { get; }
    string? ClientId { get; }
    IReadOnlyCollection<string> Scopes { get; }
    string? TokenId { get; }

    /// <summary>Caller network source (IP + optional device id) used for rate limiting.</summary>
    string SourceKey { get; }
    string? IpAddress { get; }
    Language Language { get; }
}

public interface ICorrelationContext
{
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
}

public readonly record struct RateLimitDecision(bool Allowed, long Count, TimeSpan RetryAfter);

public interface IRateLimiter
{
    /// <summary>Counts one attempt for the key and reports whether it is still within the permits for the window.</summary>
    Task<RateLimitDecision> HitAsync(string key, int permits, TimeSpan window, CancellationToken ct = default);

    Task<long> CountAsync(string key, CancellationToken ct = default);

    Task ResetAsync(string key, CancellationToken ct = default);
}

public sealed record IdempotencyEntry(string Fingerprint, bool Completed, bool IsSuccess, string? PayloadJson, string? ErrorJson);

public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> GetAsync(string scope, string key, CancellationToken ct = default);

    /// <summary>Reserves the key. False when it already exists (completed or in flight).</summary>
    Task<bool> TryBeginAsync(string scope, string key, string fingerprint, TimeSpan ttl, CancellationToken ct = default);

    Task CompleteAsync(string scope, string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default);

    Task ReleaseAsync(string scope, string key, CancellationToken ct = default);
}

/// <summary>Decides whether the current user may execute a request (roles, permission, MFA) and records the decision.</summary>
public interface IAccessAuthorizer
{
    Task<Result<Unit>> AuthorizeAsync(ICurrentUser user, IAuthorizedRequest request, string requestName, CancellationToken ct = default);
}

/// <summary>Localises user-facing error messages by code (Accept-Language). Implemented by each Api from resource files.</summary>
public interface IErrorMessageLocalizer
{
    string Localize(string code, string fallback, Language language);
}
