using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

// Mapping of the report configuration entities (schema "config"). Mapping only: rules stay in the domain.

internal sealed class RetentionPolicyConfiguration(bool isSqlite) : IEntityTypeConfiguration<ActivityLogRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<ActivityLogRetentionPolicy> b)
    {
        b.ToTable("RetentionPolicy", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
    }
}

internal sealed class ReportTemplateConfiguration(bool isSqlite) : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> b)
    {
        b.ToTable("ReportTemplates", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Ignore(e => e.Parameters);
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.DataSource).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.ParametersJson).IsRequired();
    }
}

internal sealed class ReportScheduleConfiguration(bool isSqlite) : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> b)
    {
        b.ToTable("ReportSchedules", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Ignore(e => e.Recipients);
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.Interval).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.CronText).HasMaxLength(100);
        b.Property(e => e.RecipientsCsv).HasMaxLength(4000).IsRequired();
        b.Property(e => e.Format).HasConversion<string>().HasMaxLength(8).IsRequired();
        b.HasIndex(e => new { e.IsActive, e.NextRunAtUtc }).HasDatabaseName("IX_ReportSchedules_Active_NextRun");
    }
}

internal sealed class SavedReportConfiguration(bool isSqlite) : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> b)
    {
        b.ToTable("SavedReports", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Ignore(e => e.Definition);
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
        b.Property(e => e.DefinitionJson).IsRequired();
        b.HasIndex(e => new { e.OwnerId, e.CreatedAtUtc }).HasDatabaseName("IX_SavedReports_Owner_Created");
        b.HasIndex(e => new { e.IsArchived, e.RetainUntilUtc }).HasDatabaseName("IX_SavedReports_Retention");
    }
}

internal sealed class ReportExportConfiguration(bool isSqlite) : IEntityTypeConfiguration<ReportExport>
{
    public void Configure(EntityTypeBuilder<ReportExport> b)
    {
        b.ToTable("ReportExports", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Ignore(e => e.IsInProgress);
        b.Ignore(e => e.Parameters);
        b.Property(e => e.RefKind).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.Format).HasConversion<string>().HasMaxLength(8).IsRequired();
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.ParametersHash).HasMaxLength(64).IsRequired();
        b.Property(e => e.ParametersJson).IsRequired();
        b.Property(e => e.ResultRef).HasMaxLength(1000);
        b.Property(e => e.FailureReason).HasMaxLength(500);
        // INV-06 final guard: one Queued/Generating export per administrator and identical request.
        b.HasIndex(e => new { e.RequestedBy, e.ParametersHash }).IsUnique().HasFilter("[Status] IN ('Queued','Generating')").HasDatabaseName("UQ_ReportExports_Running");
    }
}

internal sealed class ReportExportFileConfiguration : IEntityTypeConfiguration<ReportExportFile>
{
    public void Configure(EntityTypeBuilder<ReportExportFile> b)
    {
        b.ToTable("ReportExportFiles", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("ExportId").ValueGeneratedNever();
        b.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        b.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        b.Property(e => e.Content).IsRequired();
    }
}

internal sealed class ReportAccessRuleConfiguration(bool isSqlite) : IEntityTypeConfiguration<ReportAccessRule>
{
    public void Configure(EntityTypeBuilder<ReportAccessRule> b)
    {
        b.ToTable("ReportAccessRules", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Ignore(e => e.Categories);
        b.Property(e => e.Role).HasMaxLength(100).IsRequired();
        b.Property(e => e.CategoriesCsv).HasMaxLength(200).IsRequired();
        b.HasIndex(e => e.Role).IsUnique().HasDatabaseName("UQ_ReportAccessRules_Role");
    }
}

internal sealed class ReportAccessDecisionConfiguration(bool isSqlite) : IEntityTypeConfiguration<ReportAccessDecision>
{
    public void Configure(EntityTypeBuilder<ReportAccessDecision> b)
    {
        b.ToTable("ReportAccessDecisions", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Property(e => e.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(e => e.Request).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.DecidedAtUtc).HasDatabaseName("IX_ReportAccessDecisions_DecidedAt");
    }
}

internal sealed class PerformanceAlertRuleConfiguration(bool isSqlite) : IEntityTypeConfiguration<PerformanceAlertRule>
{
    public void Configure(EntityTypeBuilder<PerformanceAlertRule> b)
    {
        b.ToTable("PerformanceAlertRules", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Property(e => e.Metric).HasMaxLength(64).IsRequired();
        b.Property(e => e.Comparator).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(e => e.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.Threshold).HasPrecision(18, 4);
    }
}

internal sealed class PerformanceAlertConfiguration(bool isSqlite) : IEntityTypeConfiguration<PerformanceAlert>
{
    public void Configure(EntityTypeBuilder<PerformanceAlert> b)
    {
        b.ToTable("PerformanceAlerts", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Property(e => e.Metric).HasMaxLength(64).IsRequired();
        b.Property(e => e.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(e => e.Value).HasPrecision(18, 4);
        b.Property(e => e.Threshold).HasPrecision(18, 4);
        b.HasIndex(e => new { e.RuleId, e.RaisedAtUtc }).HasDatabaseName("IX_PerformanceAlerts_Rule_RaisedAt");
        b.HasIndex(e => e.RetainUntilUtc).HasDatabaseName("IX_PerformanceAlerts_Retention");
    }
}

internal sealed class LaborMarketReportConfiguration(bool isSqlite) : IEntityTypeConfiguration<LaborMarketReport>
{
    public void Configure(EntityTypeBuilder<LaborMarketReport> b)
    {
        b.ToTable("LaborMarketReports", ReportingDbContext.ConfigSchema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.ConfigureAggregate(isSqlite);
        b.Property(e => e.Period).HasMaxLength(7).IsRequired();
        b.Property(e => e.ContentJson).IsRequired();
        b.HasIndex(e => e.Period).IsUnique().HasDatabaseName("UQ_LaborMarketReports_Period");
    }
}
