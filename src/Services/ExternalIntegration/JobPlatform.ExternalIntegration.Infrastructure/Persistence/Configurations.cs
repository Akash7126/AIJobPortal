using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.ExternalIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

internal sealed class ExternalJobSiteIntegrationConfiguration(bool isSqlite) : IEntityTypeConfiguration<ExternalJobSiteIntegration>
{
    public void Configure(EntityTypeBuilder<ExternalJobSiteIntegration> builder)
    {
        builder.ToTable("Integrations", ExternalIntegrationDbContext.Schema);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.OwnsOne(i => i.SourcePlatform, sp =>
        {
            sp.Property(x => x.Id).HasColumnName("SourcePlatformId");
            sp.Property(x => x.Name).HasColumnName("SourcePlatformName").HasMaxLength(200).IsRequired();
            sp.Property(x => x.BaseUrl).HasColumnName("BaseUrl").HasMaxLength(500).IsRequired();
            sp.HasIndex(x => x.Id).IsUnique().HasDatabaseName("UQ_Integrations_SourcePlatformId");
        });
        builder.Navigation(i => i.SourcePlatform).IsRequired();

        builder.OwnsOne(i => i.Models, m =>
        {
            m.Property(x => x.PullEnabled).HasColumnName("PullEnabled");
            m.Property(x => x.PushEnabled).HasColumnName("PushEnabled");
        });
        builder.Navigation(i => i.Models).IsRequired();

        builder.OwnsOne(i => i.Schedule, s =>
        {
            s.Property(x => x.Mode).HasColumnName("SyncMode").HasConversion<string>().HasMaxLength(16);
            s.Property(x => x.Cron).HasColumnName("SyncCron").HasMaxLength(100);
        });
        builder.Navigation(i => i.Schedule).IsRequired();

        builder.Property(i => i.Recommendation).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.AdmissionStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.ApprovalBasis).HasMaxLength(500);
        builder.Property(i => i.AttributionVisibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.Sandbox).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasIndex(i => i.PartnerAccountId).IsUnique().HasDatabaseName("UQ_Integrations_PartnerAccountId");

        builder.HasMany(i => i.SyncRuns).WithOne().HasForeignKey("ExternalJobSiteIntegrationId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.SyncRuns).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("SyncRuns", ExternalIntegrationDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.ErrorCode).HasMaxLength(64);
        builder.HasIndex("ExternalJobSiteIntegrationId", nameof(SyncRun.StartedAtUtc)).HasDatabaseName("IX_SyncRuns_Integration_StartedAt");
    }
}

internal sealed class JobDataConfiguration(bool isSqlite) : IEntityTypeConfiguration<JobData>
{
    private sealed record StandardJobDto(string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
        DateTime? DeadlineUtc, string Location, string? SourceUrl);

    public void Configure(EntityTypeBuilder<JobData> builder)
    {
        builder.ToTable("JobData", ExternalIntegrationDbContext.Schema);
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.Property(j => j.SourceJobId).HasMaxLength(100).IsRequired();
        builder.Property(j => j.PlatformJobId).HasMaxLength(64);
        builder.Property(j => j.RawPayload).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(j => j.Model).HasConversion<string>().HasMaxLength(8).IsRequired();

        builder.Property(j => j.Standardized).HasColumnName("StandardizedJson")
            .HasConversion(new ValueConverter<StandardJob?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(ToDto(v), Json.Options),
                s => s == null ? null : ToStandardJob(JsonSerializer.Deserialize<StandardJobDto>(s, Json.Options)!)));

        builder.HasIndex(j => new { j.SourcePlatformId, j.SourceJobId }).IsUnique().HasDatabaseName("UQ_JobData_SourcePlatform_SourceJob");
        // PlatformJobId is only unique among non-null values; enforced at the application level (repository lookup before Add), not a
        // DB constraint, since a provider-portable filtered unique index needs different syntax on SQL Server vs SQLite (mirrors BC-09's
        // documented INV-03 decision) - see the BC-02 status doc.
        builder.HasIndex(j => j.PlatformJobId).HasDatabaseName("IX_JobData_PlatformJobId");
    }

    private static StandardJobDto ToDto(StandardJob v) =>
        new(v.Title, v.Summary, v.Skills, v.ContractType, v.WorkFormat, v.DeadlineUtc, v.Location, v.SourceUrl);

    private static StandardJob ToStandardJob(StandardJobDto d) => new(d.Title, d.Summary, d.Skills, d.ContractType, d.WorkFormat, d.DeadlineUtc,
        d.Location, d.SourceUrl);
}

internal sealed class JobPostAttributionConfiguration(bool isSqlite) : IEntityTypeConfiguration<JobPostAttribution>
{
    public void Configure(EntityTypeBuilder<JobPostAttribution> builder)
    {
        builder.ToTable("JobPostAttributions", ExternalIntegrationDbContext.Schema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.Property(a => a.PlatformJobId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.SourcePlatformName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Backlink).HasMaxLength(1000);
        builder.Property(a => a.Description).HasMaxLength(10_000);
        builder.Property(a => a.SyncState).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasIndex(a => a.PlatformJobId).IsUnique().HasDatabaseName("UQ_JobPostAttributions_PlatformJobId");
        builder.HasIndex(a => a.JobDataId).IsUnique().HasDatabaseName("UQ_JobPostAttributions_JobDataId");
    }
}

internal sealed class JobDataMappingConfiguration(bool isSqlite) : IEntityTypeConfiguration<JobDataMapping>
{
    private sealed record RuleDto(string SourceField, string TargetField, MappingTransform Transform);

    public void Configure(EntityTypeBuilder<JobDataMapping> builder)
    {
        builder.ToTable("JobDataMappings", ExternalIntegrationDbContext.Schema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.Property(m => m.StandardSchemaVersion).HasMaxLength(16).IsRequired();
        builder.Property(m => m.Rules).HasColumnName("RulesJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(r => new RuleDto(r.SourceField, r.TargetField, r.Transform)), Json.Options),
                s => JsonSerializer.Deserialize<List<RuleDto>>(s, Json.Options)!.Select(r => new MappingRule(r.SourceField, r.TargetField, r.Transform))
                    .ToList(),
                new ValueComparer<IReadOnlyList<MappingRule>>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, r) => HashCode.Combine(h, r.SourceField, r.TargetField, r.Transform)),
                    v => v.ToList()));

        builder.HasIndex(m => m.IntegrationId).IsUnique().HasDatabaseName("UQ_JobDataMappings_IntegrationId");
    }
}

internal sealed class ApiVersionConfiguration(bool isSqlite) : IEntityTypeConfiguration<ApiVersion>
{
    public void Configure(EntityTypeBuilder<ApiVersion> builder)
    {
        builder.ToTable("ApiVersions", ExternalIntegrationDbContext.Schema);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasMaxLength(16).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(v => v.AcceptedFormats).HasColumnName("AcceptedFormatsJson")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json.Options),
                s => JsonSerializer.Deserialize<List<string>>(s, Json.Options)!,
                new ValueComparer<IReadOnlyList<string>>((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, HashCode.Combine), v => v.ToList()));
    }
}

internal sealed class SoftwareInterfaceConnectionConfiguration(bool isSqlite) : IEntityTypeConfiguration<SoftwareInterfaceConnection>
{
    public void Configure(EntityTypeBuilder<SoftwareInterfaceConnection> builder)
    {
        builder.ToTable("SoftwareInterfaces", ExternalIntegrationDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Endpoint).HasMaxLength(500).IsRequired();

        builder.HasIndex(c => new { c.Category, c.Name }).IsUnique().HasDatabaseName("UQ_SoftwareInterfaces_Category_Name");
    }
}

internal sealed class PartnerCredentialConfiguration : IEntityTypeConfiguration<PartnerCredential>
{
    public void Configure(EntityTypeBuilder<PartnerCredential> builder)
    {
        builder.ToTable("PartnerCredentials", ExternalIntegrationDbContext.Schema);
        builder.HasKey(c => c.ApiCredentialId);
        builder.Property(c => c.ApiCredentialId).ValueGeneratedNever();
        builder.HasIndex(c => c.AccountId).HasDatabaseName("IX_PartnerCredentials_AccountId");
    }
}

internal sealed class KnownPartnerAccountConfiguration : IEntityTypeConfiguration<KnownPartnerAccount>
{
    public void Configure(EntityTypeBuilder<KnownPartnerAccount> builder)
    {
        builder.ToTable("KnownPartnerAccounts", ExternalIntegrationDbContext.Schema);
        builder.HasKey(a => a.AccountId);
        builder.Property(a => a.AccountId).ValueGeneratedNever();
    }
}

internal sealed class ApiSchemaAccessLogConfiguration(bool isSqlite) : IEntityTypeConfiguration<ApiSchemaAccessLog>
{
    public void Configure(EntityTypeBuilder<ApiSchemaAccessLog> builder)
    {
        builder.ToTable("ApiSchemaAccessLogs", ExternalIntegrationDbContext.Schema);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(l => l.ApiVersion).HasMaxLength(16).IsRequired();
        builder.HasIndex(l => l.ViewedAtUtc).HasDatabaseName("IX_ApiSchemaAccessLogs_ViewedAt");
    }
}
