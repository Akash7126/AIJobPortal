using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;

namespace JobPlatform.PlatformAdministration.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8). The read side never uses them (see IAdminReadStore); the unit of work commits.</summary>
public interface IPlatformEntityRecordRepository
{
    Task<PlatformEntityRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>INV-02: is there already a record of this type with the natural key?</summary>
    Task<bool> ExistsByKeyAsync(PlatformEntityType type, string identityKey, CancellationToken ct = default);

    void Add(PlatformEntityRecord record);
}

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);

    void Add(SystemSetting setting);
}

public interface IReferenceFileRepository
{
    Task<ReferenceFile?> GetByTypeAsync(ReferenceFileType type, CancellationToken ct = default);

    void Add(ReferenceFile file);
}

public interface IPlatformTaxonomyRepository
{
    Task<PlatformTaxonomy?> GetByTypeAsync(string type, CancellationToken ct = default);

    void Add(PlatformTaxonomy taxonomy);

    /// <summary>Stores the snapshot of a saved version (immutable per version).</summary>
    void AddSnapshot(TaxonomySnapshot snapshot);
}

public interface IJobOfferingRepository
{
    Task<JobOffering?> GetByIdAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(JobOffering offering);
}
