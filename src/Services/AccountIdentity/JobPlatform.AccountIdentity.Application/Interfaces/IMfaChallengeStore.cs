using JobPlatform.AccountIdentity.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Short-lived proof that the password step succeeded and the second factor is pending.</summary>
public interface IMfaChallengeStore
{
    Task<MfaChallengeToken> CreateAsync(Guid accountId, CancellationToken ct = default);

    Task<Guid?> GetAccountIdAsync(string token, CancellationToken ct = default);

    Task DeleteAsync(string token, CancellationToken ct = default);
}
