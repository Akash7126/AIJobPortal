using JobPlatform.AccountIdentity.Domain.Interfaces.Services;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>One-time codes for the e-mail verification login mechanism (stored hashed, capped attempts, TTL).</summary>
public interface ILoginCodeStore
{
    Task StoreAsync(Guid accountId, string codeHash, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> VerifyAndConsumeAsync(Guid accountId, string code, ISecretVerifier verifier, int maxAttempts, CancellationToken ct = default);
}
