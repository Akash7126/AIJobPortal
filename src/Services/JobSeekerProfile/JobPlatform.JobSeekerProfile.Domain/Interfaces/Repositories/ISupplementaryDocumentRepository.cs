using JobPlatform.JobSeekerProfile.Domain.Common;

namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface ISupplementaryDocumentRepository
{
    Task<SupplementaryDocument?> GetByHashAsync(DocumentOwnerType ownerType, Guid ownerId, string sha256, CancellationToken ct = default);
    Task<IReadOnlyList<SupplementaryDocument>> ListByOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task<SupplementaryDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(SupplementaryDocument document);
    void Remove(SupplementaryDocument document);
}
