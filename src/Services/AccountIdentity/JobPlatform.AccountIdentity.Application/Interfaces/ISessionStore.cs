using JobPlatform.AccountIdentity.Domain.Sessions;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Redis-backed session state: sliding idle timeout, refresh-token rotation, immediate invalidation.</summary>
public interface ISessionStore
{
    Task CreateAsync(UserSession session, CancellationToken ct = default);

    Task<UserSession?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Persists a changed session (touch, refresh rotation, invalidation) and re-arms its idle TTL.</summary>
    Task SaveAsync(UserSession session, CancellationToken ct = default);

    Task InvalidateAllForAccountAsync(Guid accountId, Guid? exceptSessionId = null, CancellationToken ct = default);

    Task RevokeTokenAsync(string tokenId, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken ct = default);

    Task RevokeClientAsync(string clientId, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> IsClientRevokedAsync(string clientId, CancellationToken ct = default);
}
