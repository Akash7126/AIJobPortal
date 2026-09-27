using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

public sealed class AdminDbContext : BaseDbContext
{
    public const string Schema = "admin";

    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    public DbSet<PlatformEntityRecord> PlatformEntityRecords => Set<PlatformEntityRecord>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<SystemSettingChange> SystemSettingHistory => Set<SystemSettingChange>();
    public DbSet<ReferenceFile> ReferenceFiles => Set<ReferenceFile>();
    public DbSet<ReferenceEntry> ReferenceEntries => Set<ReferenceEntry>();
    public DbSet<PlatformTaxonomy> Taxonomies => Set<PlatformTaxonomy>();
    public DbSet<TaxonomyNode> TaxonomyNodes => Set<TaxonomyNode>();
    public DbSet<TaxonomySnapshot> TaxonomyVersions => Set<TaxonomySnapshot>();
    public DbSet<JobOffering> JobOfferings => Set<JobOffering>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new PlatformEntityRecordConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SystemSettingConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SystemSettingChangeConfiguration());
        modelBuilder.ApplyConfiguration(new ReferenceFileConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ReferenceEntryConfiguration());
        modelBuilder.ApplyConfiguration(new PlatformTaxonomyConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new TaxonomyNodeConfiguration());
        modelBuilder.ApplyConfiguration(new TaxonomySnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new JobOfferingConfiguration(IsSqlite));
        base.OnModelCreating(modelBuilder);
    }
}
