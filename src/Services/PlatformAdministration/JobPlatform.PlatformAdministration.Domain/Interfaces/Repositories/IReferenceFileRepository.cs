using JobPlatform.PlatformAdministration.Domain.Reference;

namespace JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;

public interface IReferenceFileRepository
{
    Task<ReferenceFile?> GetByTypeAsync(ReferenceFileType type, CancellationToken ct = default);

    void Add(ReferenceFile file);
}
