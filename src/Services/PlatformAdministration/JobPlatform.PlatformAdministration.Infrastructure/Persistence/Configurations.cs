using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ReferenceEntry = JobPlatform.PlatformAdministration.Domain.Reference.ReferenceEntry;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ValueComparer<T> Comparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        v => JsonSerializer.Serialize(v, Options).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!);
}

/// <summary>Mapping only (conversions, indexes, constraints). Business rules stay in the domain.</summary>
internal sealed class PlatformEntityRecordConfiguration(bool isSqlite) : IEntityTypeConfiguration<PlatformEntityRecord>
{
    public void Configure(EntityTypeBuilder<PlatformEntityRecord> builder)
    {
        builder.ToTable("PlatformEntityRecords", AdminDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(r => r.EntityType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.IdentityKey).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Core).HasColumnName("CoreJson").IsRequired()
            .HasConversion(new ValueConverter<EntityCore, string>(
                v => JsonSerializer.Serialize(v.Fields, Json.Options),
                s => EntityCore.From(JsonSerializer.Deserialize<Dictionary<string, string>>(s, Json.Options))));
        // INV-02: E-AUM-DUPLICATE is also enforced by the database, so two concurrent creations cannot both win.
        builder.HasIndex(r => new { r.EntityType, r.IdentityKey }).IsUnique().HasDatabaseName("UQ_PlatformEntityRecords_Type_Key");
    }
}

internal sealed class SystemSettingConfiguration(bool isSqlite) : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings", AdminDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired();
        builder.Property(s => s.ValueType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.Value).HasMaxLength(500).IsRequired();
        builder.Property(s => s.Bounds).HasColumnName("BoundsJson").IsRequired()
            .HasConversion(new ValueConverter<SettingBounds, string>(
                v => JsonSerializer.Serialize(new BoundsDto(v.Min, v.Max, v.AllowedValues, v.MaxLength), Json.Options),
                s => ToBounds(JsonSerializer.Deserialize<BoundsDto>(s, Json.Options)!)));
        builder.HasIndex(s => s.Key).IsUnique().HasDatabaseName("UQ_SystemSettings_Key");
        builder.HasMany(s => s.History).WithOne().HasForeignKey(h => h.SettingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private sealed record BoundsDto(decimal? Min, decimal? Max, IReadOnlyList<string> Allowed, int? MaxLength);

    private static SettingBounds ToBounds(BoundsDto dto) => new(dto.Min, dto.Max, dto.Allowed ?? Array.Empty<string>(), dto.MaxLength);
}

internal sealed class SystemSettingChangeConfiguration : IEntityTypeConfiguration<SystemSettingChange>
{
    public void Configure(EntityTypeBuilder<SystemSettingChange> builder)
    {
        builder.ToTable("SystemSettingHistory", AdminDbContext.Schema);
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.OldValue).HasMaxLength(500).IsRequired();
        builder.Property(h => h.NewValue).HasMaxLength(500).IsRequired();
        builder.HasIndex(h => new { h.SettingId, h.ChangedAtUtc }).HasDatabaseName("IX_SystemSettingHistory_Setting_At");
    }
}

internal sealed class ReferenceFileConfiguration(bool isSqlite) : IEntityTypeConfiguration<ReferenceFile>
{
    public void Configure(EntityTypeBuilder<ReferenceFile> builder)
    {
        builder.ToTable("ReferenceFiles", AdminDbContext.Schema);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(f => f.Type).IsUnique().HasDatabaseName("UQ_ReferenceFiles_Type");
        builder.HasMany(f => f.Entries).WithOne().HasForeignKey("ReferenceFileId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReferenceEntryConfiguration : IEntityTypeConfiguration<ReferenceEntry>
{
    public void Configure(EntityTypeBuilder<ReferenceEntry> builder)
    {
        builder.ToTable("ReferenceEntries", AdminDbContext.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.OwnsOne(e => e.Name, n =>
        {
            n.Property(x => x.Ar).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
            n.Property(x => x.En).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
        });
        builder.Navigation(e => e.Name).IsRequired();
        builder.HasIndex("ReferenceFileId", nameof(ReferenceEntry.Code)).IsUnique().HasDatabaseName("UQ_ReferenceEntries_File_Code");
    }
}

internal sealed class PlatformTaxonomyConfiguration(bool isSqlite) : IEntityTypeConfiguration<PlatformTaxonomy>
{
    public void Configure(EntityTypeBuilder<PlatformTaxonomy> builder)
    {
        builder.ToTable("Taxonomies", AdminDbContext.Schema);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(t => t.Type).HasMaxLength(TaxonomyTypes.MaxLength).IsRequired();
        builder.HasIndex(t => t.Type).IsUnique().HasDatabaseName("UQ_Taxonomies_Type");
        builder.HasMany(t => t.Nodes).WithOne().HasForeignKey("TaxonomyId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Nodes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TaxonomyNodeConfiguration : IEntityTypeConfiguration<TaxonomyNode>
{
    public void Configure(EntityTypeBuilder<TaxonomyNode> builder)
    {
        builder.ToTable("TaxonomyNodes", AdminDbContext.Schema);
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Code).HasMaxLength(100).IsRequired();
        builder.Property(n => n.ParentCode).HasMaxLength(100);
        builder.OwnsOne(n => n.Name, o =>
        {
            o.Property(x => x.Ar).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
            o.Property(x => x.En).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
        });
        builder.Navigation(n => n.Name).IsRequired();
        builder.Property(n => n.Synonyms).HasColumnName("SynonymsJson").IsRequired()
            .HasConversion(new ValueConverter<IReadOnlyList<string>, string>(
                v => JsonSerializer.Serialize(v, Json.Options),
                s => JsonSerializer.Deserialize<string[]>(s, Json.Options)!),
                Json.Comparer<IReadOnlyList<string>>());
        builder.HasIndex("TaxonomyId", nameof(TaxonomyNode.Code)).IsUnique().HasDatabaseName("UQ_TaxonomyNodes_Taxonomy_Code");
    }
}

internal sealed class TaxonomySnapshotConfiguration : IEntityTypeConfiguration<TaxonomySnapshot>
{
    public void Configure(EntityTypeBuilder<TaxonomySnapshot> builder)
    {
        builder.ToTable("TaxonomyVersions", AdminDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Type).HasMaxLength(TaxonomyTypes.MaxLength).IsRequired();
        builder.Property(s => s.Nodes).HasColumnName("NodesJson").IsRequired()
            .HasConversion(new ValueConverter<IReadOnlyList<TaxonomyNodeData>, string>(
                v => JsonSerializer.Serialize(v, Json.Options),
                s => JsonSerializer.Deserialize<TaxonomyNodeData[]>(s, Json.Options)!),
                Json.Comparer<IReadOnlyList<TaxonomyNodeData>>());
        // Immutable per version: one snapshot per (taxonomy, version).
        builder.HasIndex(s => new { s.TaxonomyId, s.TaxonomyVersion }).IsUnique().HasDatabaseName("UQ_TaxonomyVersions_Taxonomy_Version");
    }
}

internal sealed class JobOfferingConfiguration(bool isSqlite) : IEntityTypeConfiguration<JobOffering>
{
    public void Configure(EntityTypeBuilder<JobOffering> builder)
    {
        builder.ToTable("JobOfferings", AdminDbContext.Schema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(o => o.Title).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(o => o.Moderation).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.Reason).HasMaxLength(500);
        builder.HasIndex(o => o.Status).HasDatabaseName("IX_JobOfferings_Status");
    }
}
