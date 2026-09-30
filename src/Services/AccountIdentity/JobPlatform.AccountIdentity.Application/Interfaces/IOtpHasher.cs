using JobPlatform.AccountIdentity.Domain.Interfaces.Services;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Keyed hash for short-lived one-time codes (activation OTP, e-mail codes).</summary>
public interface IOtpHasher : ISecretVerifier
{
    string Hash(string code);
}
