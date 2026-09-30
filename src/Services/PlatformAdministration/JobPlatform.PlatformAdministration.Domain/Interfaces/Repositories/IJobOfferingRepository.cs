using JobPlatform.PlatformAdministration.Domain.Offerings;

namespace JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;

public interface IJobOfferingRepository
{
    Task<JobOffering?> GetByIdAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(JobOffering offering);
}
