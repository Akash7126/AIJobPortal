namespace JobPlatform.AccountIdentity.Application.Interfaces;

public interface ISecretGenerator
{
    /// <summary>Six decimal digits from a cryptographic RNG.</summary>
    string GenerateOtp();

    string GenerateApiKeyId();

    string GenerateApiSecret();

    /// <summary>URL-safe random token (refresh secret, e-mail verification token, MFA challenge).</summary>
    string GenerateToken();
}
