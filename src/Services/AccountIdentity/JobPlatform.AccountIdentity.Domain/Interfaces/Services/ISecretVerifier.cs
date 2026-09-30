namespace JobPlatform.AccountIdentity.Domain.Interfaces.Services;

/// <summary>Verifies a plaintext secret (password, OTP, API secret) against its stored hash. Implemented in Infrastructure; keeps crypto out of the domain.</summary>
public interface ISecretVerifier
{
    bool Verify(string secret, string hash);
}
