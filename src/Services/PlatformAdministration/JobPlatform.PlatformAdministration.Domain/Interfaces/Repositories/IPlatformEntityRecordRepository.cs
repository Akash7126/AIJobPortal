using JobPlatform.PlatformAdministration.Domain.Entities;

namespace JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8). The read side never uses them (see IAdminReadStore); the unit of work commits.</summary>
public interface IPlatformEntityRecordRepository
{
    Task<PlatformEntityRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>INV-02: is there already a record of this type with the natural key?</summary>
    Task<bool> ExistsByKeyAsync(PlatformEntityType type, string identityKey, CancellationToken ct = default);

    void Add(PlatformEntityRecord record);
}
