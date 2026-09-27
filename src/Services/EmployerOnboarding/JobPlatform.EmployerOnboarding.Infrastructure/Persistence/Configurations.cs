using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.EmployerOnboarding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>Mapping only (conversions, indexes, constraints). Business rules stay in the domain.</summary>
internal sealed class EmployerRegistrationConfiguration(bool isSqlite) : IEntityTypeConfiguration<EmployerRegistration>
{
    private sealed record IdentityDto(string Name, string CompanyId, string RegistrationNumber);

    private sealed record Level2Dto(string Website, string Industry, CompanySize Size, string Governorate, string City, string? Street, string Description);

    public void Configure(EntityTypeBuilder<EmployerRegistration> builder)
    {
        builder.ToTable("EmployerRegistrations", EmployerOnboardingDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.CompanyIdentity).HasColumnName("CompanyIdentityJson")
            .HasConversion(new ValueConverter<CompanyIdentity?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(new IdentityDto(v.Name, v.CompanyId, v.RegistrationNumber), Json.Options),
                s => s == null ? null : ToIdentity(JsonSerializer.Deserialize<IdentityDto>(s, Json.Options)!)));
        builder.Property(r => r.Level2).HasColumnName("Level2Json")
            .HasConversion(new ValueConverter<Level2Details?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(
                    new Level2Dto(v.Website, v.Industry, v.Size, v.Address.Governorate, v.Address.City, v.Address.Street, v.Description), Json.Options),
                s => s == null ? null : ToLevel2(JsonSerializer.Deserialize<Level2Dto>(s, Json.Options)!)));
        builder.HasIndex(r => r.EmployerAccountId).IsUnique().HasDatabaseName("UQ_EmployerRegistrations_EmployerAccountId");
    }

    private static CompanyIdentity ToIdentity(IdentityDto d) => new(d.Name, d.CompanyId, d.RegistrationNumber);

    private static Level2Details ToLevel2(Level2Dto d) => new(d.Website, d.Industry, d.Size, new Address(d.Governorate, d.City, d.Street), d.Description);
}

internal sealed class CompanyMediaAndDocumentConfiguration(bool isSqlite) : IEntityTypeConfiguration<CompanyMediaAndDocument>
{
    public void Configure(EntityTypeBuilder<CompanyMediaAndDocument> builder)
    {
        builder.ToTable("CompanyMedia", EmployerOnboardingDbContext.Schema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.OwnsOne(m => m.File, f =>
        {
            f.Property(x => x.StorageKey).HasColumnName("StorageKey").HasMaxLength(500).IsRequired();
            f.Property(x => x.FileName).HasColumnName("FileName").HasMaxLength(260).IsRequired();
            f.Property(x => x.SizeBytes).HasColumnName("SizeBytes").IsRequired();
            f.Property(x => x.ContentType).HasColumnName("ContentType").HasMaxLength(100).IsRequired();
            f.Property(x => x.Sha256).HasColumnName("Sha256").HasMaxLength(64).IsRequired();
        });
        builder.Navigation(m => m.File).IsRequired();
        // INV-07 duplicate detection (per employer + file hash, non-removed items only) is enforced at the application level
        // (GetByHashAsync before Attach) rather than a DB constraint, since the hash lives on an owned sub-entity mapped into this table.
        builder.HasIndex(m => m.EmployerAccountId).HasDatabaseName("IX_CompanyMedia_EmployerAccountId");
    }
}

internal sealed class EmployerStandingConfiguration(bool isSqlite) : IEntityTypeConfiguration<EmployerStanding>
{
    public void Configure(EntityTypeBuilder<EmployerStanding> builder)
    {
        builder.ToTable("EmployerStandings", EmployerOnboardingDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.HasIndex(s => s.EmployerAccountId).IsUnique().HasDatabaseName("UQ_EmployerStandings_EmployerAccountId");
        builder.HasMany(s => s.BadgeAudit).WithOne().HasForeignKey("EmployerStandingId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.BadgeAudit).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BadgeAuditEntryConfiguration : IEntityTypeConfiguration<BadgeAuditEntry>
{
    public void Configure(EntityTypeBuilder<BadgeAuditEntry> builder)
    {
        builder.ToTable("BadgeAudit", EmployerOnboardingDbContext.Schema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Change).HasMaxLength(64).IsRequired();
        builder.HasIndex("EmployerStandingId", nameof(BadgeAuditEntry.AtUtc)).HasDatabaseName("IX_BadgeAudit_Standing_At");
    }
}

internal sealed class KnownAccountConfiguration : IEntityTypeConfiguration<KnownAccount>
{
    public void Configure(EntityTypeBuilder<KnownAccount> builder)
    {
        builder.ToTable("KnownAccounts", EmployerOnboardingDbContext.Schema);
        builder.HasKey(a => a.AccountId);
        builder.Property(a => a.AccountId).ValueGeneratedNever();
    }
}
