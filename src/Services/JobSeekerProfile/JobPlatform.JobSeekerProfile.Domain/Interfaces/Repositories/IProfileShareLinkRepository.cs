namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface IProfileShareLinkRepository
{
    Task<ProfileShareLink?> GetActiveByProfileAsync(Guid profileId, CancellationToken ct = default);
    Task<ProfileShareLink?> GetByTokenAsync(string token, CancellationToken ct = default);
    void Add(ProfileShareLink link);
}
