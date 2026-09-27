using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class PlatformEntityRecordRepository(AdminDbContext db) : IPlatformEntityRecordRepository
{
    public Task<PlatformEntityRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.PlatformEntityRecords.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExistsByKeyAsync(PlatformEntityType type, string identityKey, CancellationToken ct = default) =>
        db.PlatformEntityRecords.AnyAsync(r => r.EntityType == type && r.IdentityKey == identityKey, ct);

    public void Add(PlatformEntityRecord record) => db.PlatformEntityRecords.Add(record);
}

internal sealed class SystemSettingRepository(AdminDbContext db) : ISystemSettingRepository
{
    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public void Add(SystemSetting setting) => db.SystemSettings.Add(setting);
}

internal sealed class ReferenceFileRepository(AdminDbContext db) : IReferenceFileRepository
{
    public Task<ReferenceFile?> GetByTypeAsync(ReferenceFileType type, CancellationToken ct = default) =>
        db.ReferenceFiles.Include(f => f.Entries).FirstOrDefaultAsync(f => f.Type == type, ct);

    public void Add(ReferenceFile file) => db.ReferenceFiles.Add(file);
}

internal sealed class PlatformTaxonomyRepository(AdminDbContext db) : IPlatformTaxonomyRepository
{
    public Task<PlatformTaxonomy?> GetByTypeAsync(string type, CancellationToken ct = default) =>
        db.Taxonomies.Include(t => t.Nodes).FirstOrDefaultAsync(t => t.Type == type, ct);

    public void Add(PlatformTaxonomy taxonomy) => db.Taxonomies.Add(taxonomy);

    public void AddSnapshot(TaxonomySnapshot snapshot) => db.TaxonomyVersions.Add(snapshot);
}

internal sealed class JobOfferingRepository(AdminDbContext db) : IJobOfferingRepository
{
    public Task<JobOffering?> GetByIdAsync(Guid jobPostingId, CancellationToken ct = default) =>
        db.JobOfferings.FirstOrDefaultAsync(o => o.Id == jobPostingId, ct);

    public Task<bool> ExistsAsync(Guid jobPostingId, CancellationToken ct = default) => db.JobOfferings.AnyAsync(o => o.Id == jobPostingId, ct);

    public void Add(JobOffering offering) => db.JobOfferings.Add(offering);
}
