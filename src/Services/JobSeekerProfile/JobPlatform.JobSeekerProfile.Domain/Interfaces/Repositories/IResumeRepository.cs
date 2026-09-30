namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface IResumeRepository
{
    Task<Resume?> GetCurrentByProfileAsync(Guid profileId, CancellationToken ct = default);
    Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Resume resume);
}
