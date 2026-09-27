using JobPlatform.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

// Mapping of the analytics store. Columnstore indexes and monthly partitioning of the fact tables are SQL Server tuning applied in the migration
// (see the handover's D-01); the model itself stays provider neutral so SQLite dev/test runs the same code.

internal sealed class FactEventConfiguration : IEntityTypeConfiguration<FactEvent>
{
    public void Configure(EntityTypeBuilder<FactEvent> b)
    {
        b.ToTable("FactEvent", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.SourceBc).HasMaxLength(64).IsRequired();
        b.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        b.Property(e => e.ActivityType).HasMaxLength(32).IsRequired();
        b.Property(e => e.ActorType).HasMaxLength(32).IsRequired();
        b.Property(e => e.ActorKey).HasMaxLength(32).IsRequired();
        b.Property(e => e.SubjectId).HasMaxLength(64);
        b.HasIndex(e => e.MessageId).IsUnique().HasDatabaseName("UQ_FactEvent_MessageId");
        b.HasIndex(e => new { e.OccurredAtUtc, e.ActivityType }).HasDatabaseName("IX_FactEvent_OccurredAt_Activity");
    }
}

internal sealed class FactJobPostingConfiguration : IEntityTypeConfiguration<FactJobPosting>
{
    public void Configure(EntityTypeBuilder<FactJobPosting> b)
    {
        b.ToTable("FactJobPosting", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Status).HasMaxLength(32).IsRequired();
        b.Property(e => e.Category).HasMaxLength(100);
        b.Property(e => e.Location).HasMaxLength(100);
        b.Property(e => e.Source).HasMaxLength(32).IsRequired();
        b.Property(e => e.Title).HasMaxLength(300);
        b.Property(e => e.SalaryMin).HasPrecision(18, 2);
        b.Property(e => e.SalaryMax).HasPrecision(18, 2);
        b.HasIndex(e => e.FirstSeenAtUtc).HasDatabaseName("IX_FactJobPosting_FirstSeen");
        b.HasIndex(e => new { e.Category, e.Location }).HasDatabaseName("IX_FactJobPosting_Category_Location");
    }
}

internal sealed class FactSkillDemandConfiguration : IEntityTypeConfiguration<FactSkillDemand>
{
    public void Configure(EntityTypeBuilder<FactSkillDemand> b)
    {
        b.ToTable("FactSkillDemand", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Skill).HasMaxLength(100).IsRequired();
        b.Property(e => e.Side).HasMaxLength(16).IsRequired();
        b.HasIndex(e => new { e.Skill, e.Side, e.SubjectId }).IsUnique().HasDatabaseName("UQ_FactSkillDemand_Skill_Side_Subject");
        b.HasIndex(e => e.OccurredAtUtc).HasDatabaseName("IX_FactSkillDemand_OccurredAt");
    }
}

internal sealed class FactMatchConfiguration : IEntityTypeConfiguration<FactMatch>
{
    public void Configure(EntityTypeBuilder<FactMatch> b)
    {
        b.ToTable("FactMatch", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Kind).HasMaxLength(16).IsRequired();
        b.Property(e => e.Score).HasPrecision(9, 4);
        b.Property(e => e.ConfigVersion).HasMaxLength(64);
        b.HasIndex(e => e.OccurredAtUtc).HasDatabaseName("IX_FactMatch_OccurredAt");
    }
}

internal sealed class FactRegistrationConfiguration : IEntityTypeConfiguration<FactRegistration>
{
    public void Configure(EntityTypeBuilder<FactRegistration> b)
    {
        b.ToTable("FactRegistration", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Milestone).HasMaxLength(64).IsRequired();
        b.Property(e => e.ActorType).HasMaxLength(32).IsRequired();
        b.Property(e => e.Governorate).HasMaxLength(100);
        b.HasIndex(e => e.MessageId).IsUnique().HasDatabaseName("UQ_FactRegistration_MessageId");
        b.HasIndex(e => new { e.OccurredAtUtc, e.Milestone }).HasDatabaseName("IX_FactRegistration_OccurredAt_Milestone");
    }
}

internal sealed class FactNotificationConfiguration : IEntityTypeConfiguration<FactNotification>
{
    public void Configure(EntityTypeBuilder<FactNotification> b)
    {
        b.ToTable("FactNotification", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Channel).HasMaxLength(16).IsRequired();
        b.Property(e => e.Category).HasMaxLength(64).IsRequired();
        b.Property(e => e.Status).HasMaxLength(32).IsRequired();
        b.HasIndex(e => e.OccurredAtUtc).HasDatabaseName("IX_FactNotification_OccurredAt");
    }
}

internal sealed class FactSystemMetricConfiguration : IEntityTypeConfiguration<FactSystemMetric>
{
    public void Configure(EntityTypeBuilder<FactSystemMetric> b)
    {
        b.ToTable("FactSystemMetric", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Metric).HasMaxLength(64).IsRequired();
        b.Property(e => e.Value).HasPrecision(18, 4);
        b.Property(e => e.Source).HasMaxLength(64).IsRequired();
        b.HasIndex(e => new { e.Metric, e.SampledAtUtc }).HasDatabaseName("IX_FactSystemMetric_Metric_SampledAt");
    }
}

internal sealed class FactOutcomeConfiguration : IEntityTypeConfiguration<FactOutcome>
{
    public void Configure(EntityTypeBuilder<FactOutcome> b)
    {
        b.ToTable("FactOutcome", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("JobPostingId").ValueGeneratedNever();
        b.Ignore(e => e.HasFollowUp);
        b.Property(e => e.FitSum).HasPrecision(18, 4);
        b.HasIndex(e => e.FirstShortlistedAtUtc).HasDatabaseName("IX_FactOutcome_FirstShortlisted");
    }
}

internal sealed class AggDailyConfiguration : IEntityTypeConfiguration<AggDaily>
{
    public void Configure(EntityTypeBuilder<AggDaily> b)
    {
        b.ToTable("AggDaily", ReportingDbContext.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Metric).HasMaxLength(150).IsRequired();
        b.HasIndex(e => new { e.Day, e.Metric }).IsUnique().HasDatabaseName("UQ_AggDaily_Day_Metric");
    }
}
