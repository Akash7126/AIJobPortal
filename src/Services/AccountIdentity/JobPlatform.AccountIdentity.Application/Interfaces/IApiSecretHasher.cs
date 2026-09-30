using JobPlatform.AccountIdentity.Domain.Interfaces.Services;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Keyed hash for high-entropy random secrets (API secrets, refresh tokens, verification tokens).</summary>
public interface IApiSecretHasher : ISecretVerifier
{
    string Hash(string secret);
}
