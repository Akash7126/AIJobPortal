using JobPlatform.AccountIdentity.Domain.Interfaces.Services;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Adaptive one-way password hashing (Q-05: strong hashing, not "AES-256").</summary>
public interface IPasswordHasher : ISecretVerifier
{
    string Hash(string password);

    /// <summary>True when the stored hash uses weaker parameters than the current configuration (rehash on next login).</summary>
    bool NeedsRehash(string hash);

    /// <summary>Spends the same effort as a real verification. Used for unknown accounts so timing does not reveal whether a login exists.</summary>
    void SimulateVerify(string password);
}
