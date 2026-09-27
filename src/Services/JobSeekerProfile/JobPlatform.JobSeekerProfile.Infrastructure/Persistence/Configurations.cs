using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ValueComparer<T> Comparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        v => JsonSerializer.Serialize(v, Options).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!);
}

/// <summary>Mapping only (conversions, indexes, constraints). Business rules stay in the domain.
/// The unique-by-content-hash guards (documents/resume-drafts) are enforced at the Application layer, not the database, in this build -
/// see docs/bc-status/BC-04.md known limitations.</summary>
internal sealed class ProfileConfiguration(bool isSqlite) : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("Profiles", JobSeekerProfileDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.HasIndex(p => p.OwnerAccountId).IsUnique().HasDatabaseName("UQ_Profiles_OwnerAccountId");
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Gender).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.FullName).HasColumnName("FullName").HasMaxLength(100).IsRequired()
            .HasConversion(v => v.Value, s => new FullName(s));
        builder.Property(p => p.Email).HasColumnName("Email").HasMaxLength(Email.MaxLength).IsRequired()
            .HasConversion(v => v.Value, s => Email.Create(s));
        builder.Property(p => p.MobileNumber).HasColumnName("MobileNumber").HasMaxLength(20).IsRequired()
            .HasConversion(v => v.Value, s => MobileNumber.Create(s));
        builder.Property(p => p.YearsOfExperience).HasColumnType("decimal(6,2)");
        builder.Property(p => p.Statement).HasMaxLength(1000);
        builder.Property(p => p.Bio).HasMaxLength(2000);
        builder.Property(p => p.CompletionPercent);

        builder.Property<Dictionary<string, SectionState>>("_sectionStatus").HasColumnName("SectionStatusJson").IsRequired()
            .HasConversion(new ValueConverter<Dictionary<string, SectionState>, string>(
                v => JsonSerializer.Serialize(v, Json.Options),
                s => JsonSerializer.Deserialize<Dictionary<string, SectionState>>(s, Json.Options) ?? new Dictionary<string, SectionState>()),
                Json.Comparer<Dictionary<string, SectionState>>());

        builder.OwnsOne(p => p.SalaryExpectation, o =>
        {
            o.Property(s => s.Min).HasColumnName("SalaryMin").HasColumnType("decimal(12,2)");
            o.Property(s => s.Max).HasColumnName("SalaryMax").HasColumnType("decimal(12,2)");
            o.Property(s => s.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
        });
        builder.OwnsOne(p => p.Address, o =>
        {
            o.Property(a => a.Governorate).HasColumnName("Governorate").HasMaxLength(100);
            o.Property(a => a.City).HasColumnName("City").HasMaxLength(100);
            o.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200);
        });

        builder.OwnsMany(p => p.Education, e =>
        {
            e.ToTable("ProfileEducation", JobSeekerProfileDbContext.Schema);
            e.WithOwner().HasForeignKey("ProfileId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Degree).HasMaxLength(200).IsRequired();
            e.Property(x => x.Institution).HasMaxLength(200).IsRequired();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.HasIndex("ProfileId");
        });
        builder.Navigation(p => p.Education).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Experience, e =>
        {
            e.ToTable("ProfileExperience", JobSeekerProfileDbContext.Schema);
            e.WithOwner().HasForeignKey("ProfileId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Company).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(200).IsRequired();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.HasIndex("ProfileId");
        });
        builder.Navigation(p => p.Experience).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Skills, e =>
        {
            e.ToTable("ProfileSkills", JobSeekerProfileDbContext.Schema);
            e.WithOwner().HasForeignKey("ProfileId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(x => x.Class).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.HasIndex("ProfileId");
        });
        builder.Navigation(p => p.Skills).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Training, e =>
        {
            e.ToTable("ProfileTraining", JobSeekerProfileDbContext.Schema);
            e.WithOwner().HasForeignKey("ProfileId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.HasIndex("ProfileId");
        });
        builder.Navigation(p => p.Training).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Certificates, e =>
        {
            e.ToTable("ProfileCertificates", JobSeekerProfileDbContext.Schema);
            e.WithOwner().HasForeignKey("ProfileId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Issuer).HasMaxLength(200);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.HasIndex("ProfileId");
        });
        builder.Navigation(p => p.Certificates).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.SocialLinks, e =>
        {
            e.ToTable("ProfileSocialLinks", JobSeekerProfileDbContext.Schema);
            e.WithOwner().HasForeignKey("ProfileId");
            e.Property<Guid>("Id").ValueGeneratedOnAdd();
            e.HasKey("Id");
            e.Property(x => x.Network).HasMaxLength(50).IsRequired();
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
        });
        builder.Navigation(p => p.SocialLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ResumeConfiguration(bool isSqlite) : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("Resumes", JobSeekerProfileDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(r => r.Format).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.OwnsOne(r => r.File, o =>
        {
            o.Property(f => f.StorageKey).HasColumnName("StorageKey").HasMaxLength(500).IsRequired();
            o.Property(f => f.FileName).HasColumnName("FileName").HasMaxLength(260).IsRequired();
            o.Property(f => f.SizeBytes).HasColumnName("SizeBytes").IsRequired();
            o.Property(f => f.ContentType).HasColumnName("ContentType").HasMaxLength(200).IsRequired();
            o.Property(f => f.Sha256).HasColumnName("Sha256").HasMaxLength(64).IsRequired();
        });
        builder.Navigation(r => r.File).IsRequired();
        // Only one row per profile should have IsCurrent = true; enforced by the Upload/MarkSuperseded application flow, not a DB constraint here.
        builder.HasIndex(r => new { r.ProfileId, r.IsCurrent }).HasDatabaseName("IX_Resumes_Profile_Current");
    }
}

internal sealed class ProfileShareLinkConfiguration(bool isSqlite) : IEntityTypeConfiguration<ProfileShareLink>
{
    public void Configure(EntityTypeBuilder<ProfileShareLink> builder)
    {
        builder.ToTable("ShareLinks", JobSeekerProfileDbContext.Schema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(l => l.Token).HasMaxLength(32).IsRequired();
        builder.HasIndex(l => l.Token).IsUnique().HasDatabaseName("UQ_ShareLinks_Token");
        builder.HasIndex(l => new { l.ProfileId, l.IsActive }).HasDatabaseName("IX_ShareLinks_Profile_Active");
    }
}

internal sealed class SupplementaryDocumentConfiguration(bool isSqlite) : IEntityTypeConfiguration<SupplementaryDocument>
{
    public void Configure(EntityTypeBuilder<SupplementaryDocument> builder)
    {
        builder.ToTable("SupplementaryDocuments", JobSeekerProfileDbContext.Schema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(d => d.OwnerType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.DocumentType).HasMaxLength(100).IsRequired();
        builder.OwnsOne(d => d.File, o =>
        {
            o.Property(f => f.StorageKey).HasColumnName("StorageKey").HasMaxLength(500).IsRequired();
            o.Property(f => f.FileName).HasColumnName("FileName").HasMaxLength(260).IsRequired();
            o.Property(f => f.SizeBytes).HasColumnName("SizeBytes").IsRequired();
            o.Property(f => f.ContentType).HasColumnName("ContentType").HasMaxLength(200).IsRequired();
            o.Property(f => f.Sha256).HasColumnName("Sha256").HasMaxLength(64).IsRequired();
        });
        builder.Navigation(d => d.File).IsRequired();
        builder.HasIndex(d => new { d.OwnerType, d.OwnerId }).HasDatabaseName("IX_SupplementaryDocuments_Owner");
    }
}

internal sealed class JobPreferenceConfiguration : IEntityTypeConfiguration<JobPreference>
{
    public void Configure(EntityTypeBuilder<JobPreference> builder)
    {
        builder.ToTable("JobPreferences", JobSeekerProfileDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        // Deliberately no RowVersion: last-write-wins (handover section 3.5), unlike Profile's reject-on-conflict.
        // The conversion back from the provider must materialize the exact backing-field type (List<T>: field access mode sets the field
        // directly via reflection, so an array here throws InvalidCastException at read time - a bug this build's tests caught).
        builder.Property(p => p.JobTypes).HasColumnName("JobTypesJson").IsRequired().UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new ValueConverter<IReadOnlyList<string>, string>(
                v => JsonSerializer.Serialize(v, Json.Options), s => JsonSerializer.Deserialize<List<string>>(s, Json.Options)!), Json.Comparer<IReadOnlyList<string>>());
        builder.Property(p => p.Industries).HasColumnName("IndustriesJson").IsRequired().UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new ValueConverter<IReadOnlyList<string>, string>(
                v => JsonSerializer.Serialize(v, Json.Options), s => JsonSerializer.Deserialize<List<string>>(s, Json.Options)!), Json.Comparer<IReadOnlyList<string>>());
        builder.Property(p => p.Locations).HasColumnName("LocationsJson").IsRequired().UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new ValueConverter<IReadOnlyList<string>, string>(
                v => JsonSerializer.Serialize(v, Json.Options), s => JsonSerializer.Deserialize<List<string>>(s, Json.Options)!), Json.Comparer<IReadOnlyList<string>>());
        builder.Property(p => p.WorkArrangements).HasColumnName("WorkArrangementsJson").IsRequired().UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(new ValueConverter<IReadOnlyList<WorkArrangement>, string>(
                v => JsonSerializer.Serialize(v, Json.Options), s => JsonSerializer.Deserialize<List<WorkArrangement>>(s, Json.Options)!),
                Json.Comparer<IReadOnlyList<WorkArrangement>>());
        builder.OwnsOne(p => p.SalaryExpectation, o =>
        {
            o.Property(s => s.Min).HasColumnName("SalaryMin").HasColumnType("decimal(12,2)");
            o.Property(s => s.Max).HasColumnName("SalaryMax").HasColumnType("decimal(12,2)");
            o.Property(s => s.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
        });
    }
}

internal sealed class PrivacySettingConfiguration : IEntityTypeConfiguration<PrivacySetting>
{
    public void Configure(EntityTypeBuilder<PrivacySetting> builder)
    {
        builder.ToTable("PrivacySettings", JobSeekerProfileDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.DeletionState).HasConversion<string>().HasMaxLength(16).IsRequired();
    }
}

internal sealed class KnownAccountConfiguration : IEntityTypeConfiguration<KnownAccount>
{
    public void Configure(EntityTypeBuilder<KnownAccount> builder)
    {
        builder.ToTable("KnownAccounts", JobSeekerProfileDbContext.Schema);
        builder.HasKey(a => a.AccountId);
        builder.Property(a => a.AccountId).ValueGeneratedNever();
        builder.Property(a => a.ActorType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.Standing).HasMaxLength(32).IsRequired();
    }
}

internal sealed class ProcessedParsedDataConfiguration : IEntityTypeConfiguration<ProcessedParsedData>
{
    public void Configure(EntityTypeBuilder<ProcessedParsedData> builder)
    {
        builder.ToTable("ProcessedParsedData", JobSeekerProfileDbContext.Schema);
        builder.HasKey(p => p.ResumeParsedDataId);
        builder.Property(p => p.ResumeParsedDataId).ValueGeneratedNever();
    }
}
