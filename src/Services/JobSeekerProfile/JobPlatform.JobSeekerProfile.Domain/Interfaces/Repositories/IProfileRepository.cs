namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Profile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);
    Task<bool> ExistsByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);
    void Add(Profile profile);
}
