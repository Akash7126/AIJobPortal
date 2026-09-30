using JobPlatform.AccountIdentity.Domain.PasswordPolicies;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;

public interface IPasswordPolicyRepository
{
    /// <summary>Returns the singleton policy, creating the default when it does not exist yet.</summary>
    Task<PasswordPolicy> GetAsync(CancellationToken ct = default);
}
