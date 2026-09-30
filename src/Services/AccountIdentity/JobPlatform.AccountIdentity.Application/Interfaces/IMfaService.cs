namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>TOTP (RFC 6238) for multi-factor authentication (Q-08: TOTP). The seed is encrypted at rest (AES-256-GCM).</summary>
public interface IMfaService
{
    string GenerateSecret();

    string Protect(string secret);

    string Unprotect(string protectedSecret);

    string BuildProvisioningUri(string issuer, string accountLabel, string secret);

    /// <summary>Returns the TOTP time step the code matches (current step +/- 1 for clock drift), or null. The caller rejects steps it has already accepted.</summary>
    long? FindMatchingTimeStep(string secret, string code, DateTime nowUtc);
}
