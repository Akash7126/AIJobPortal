namespace JobPlatform.JobSeekerProfile.Application.Interfaces;

/// <summary>Anti-corruption port to BC-03 (Q-03, foundation 9.5): requests account deactivation/deletion.</summary>
public interface IAccountIdentityClient
{
    Task<bool> RequestDeactivationAsync(Guid accountId, string reason, string standing, CancellationToken ct = default);
}
