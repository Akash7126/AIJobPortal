using JobPlatform.AuditLogging.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence;

public sealed class AuditDbContext : BaseDbContext
{
    public const string Schema = "audit";

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SyncJobStatus> SyncJobStatuses => Set<SyncJobStatus>();
    public DbSet<IntegrationUsageDaily> IntegrationUsageDaily => Set<IntegrationUsageDaily>();
    public DbSet<JobStatusHistoryEntry> JobStatusHistory => Set<JobStatusHistoryEntry>();
    public DbSet<NotificationLogEntry> NotificationLog => Set<NotificationLogEntry>();
    public DbSet<EmployerDashboard> EmployerDashboards => Set<EmployerDashboard>();
    public DbSet<CandidateInsightRecord> CandidateInsights => Set<CandidateInsightRecord>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new SyncJobStatusConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationUsageDailyConfiguration());
        modelBuilder.ApplyConfiguration(new JobStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationLogConfiguration());
        modelBuilder.ApplyConfiguration(new EmployerDashboardConfiguration());
        modelBuilder.ApplyConfiguration(new CandidateInsightConfiguration());
        modelBuilder.ApplyConfiguration(new ExportJobConfiguration(IsSqlite));
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Mapping only (conversions, indexes, constraints). Business rules stay in the domain.</summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", AuditDbContext.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.Scope);
        builder.Ignore(e => e.Details);
        builder.Property(e => e.SourceBc).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ActorType).HasMaxLength(32);
        builder.Property(e => e.SubjectType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SubjectId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.OwnerType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(64);
        builder.Property(e => e.DetailsJson).IsRequired();

        // INV-01: a redelivery never duplicates a log line. One event can feed several logs, hence the category in the key.
        builder.HasIndex(e => new { e.SourceBc, e.SourceMessageId, e.Category }).IsUnique().HasDatabaseName("UQ_AuditEntries_Source_Category");
        builder.HasIndex(e => new { e.Category, e.OwnerId, e.OccurredAtUtc }).IsDescending(false, false, true).HasDatabaseName("IX_AuditEntries_Category_Owner_OccurredAt");
        builder.HasIndex(e => new { e.SubjectType, e.SubjectId, e.OccurredAtUtc }).HasDatabaseName("IX_AuditEntries_Subject");
        builder.HasIndex(e => new { e.IsArchived, e.RetainUntilUtc }).HasDatabaseName("IX_AuditEntries_Retention");
    }
}

internal sealed class SyncJobStatusConfiguration : IEntityTypeConfiguration<SyncJobStatus>
{
    public void Configure(EntityTypeBuilder<SyncJobStatus> builder)
    {
        builder.ToTable("SyncJobStatuses", AuditDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("PlatformJobId").HasMaxLength(128).ValueGeneratedNever();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.ReasonCode).HasMaxLength(64);
        builder.HasIndex(s => new { s.SourcePlatformId, s.Status }).HasDatabaseName("IX_SyncJobStatuses_Platform_Status");
        builder.HasIndex(s => new { s.OwnerId, s.Status }).HasDatabaseName("IX_SyncJobStatuses_Owner_Status");
    }
}

internal sealed class IntegrationUsageDailyConfiguration : IEntityTypeConfiguration<IntegrationUsageDaily>
{
    public void Configure(EntityTypeBuilder<IntegrationUsageDaily> builder)
    {
        builder.ToTable("IntegrationUsageDaily", AuditDbContext.Schema);
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.HasIndex(u => new { u.PartnerId, u.Day }).IsUnique().HasDatabaseName("UQ_IntegrationUsageDaily_Partner_Day");
    }
}

internal sealed class JobStatusHistoryConfiguration : IEntityTypeConfiguration<JobStatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<JobStatusHistoryEntry> builder)
    {
        builder.ToTable("JobStatusHistory", AuditDbContext.Schema);
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.FromStatus).HasMaxLength(32);
        builder.Property(h => h.ToStatus).HasMaxLength(32).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(500);
        builder.HasIndex(h => new { h.JobPostingId, h.ChangedAtUtc }).HasDatabaseName("IX_JobStatusHistory_Job_At");
        builder.HasIndex(h => h.SourceMessageId).IsUnique().HasDatabaseName("UQ_JobStatusHistory_SourceMessage");
    }
}

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLogEntry>
{
    public void Configure(EntityTypeBuilder<NotificationLogEntry> builder)
    {
        builder.ToTable("NotificationLog", AuditDbContext.Schema);
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Channel).HasMaxLength(16).IsRequired();
        builder.Property(n => n.Category).HasMaxLength(64).IsRequired();
        builder.Property(n => n.MaskedRecipient).HasMaxLength(128).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(200);
        builder.Property(n => n.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(n => new { n.RecipientId, n.SentAtUtc }).IsDescending(false, true).HasDatabaseName("IX_NotificationLog_Recipient_SentAt");
        builder.HasIndex(n => new { n.Channel, n.SentAtUtc }).HasDatabaseName("IX_NotificationLog_Channel_SentAt");
    }
}

internal sealed class EmployerDashboardConfiguration : IEntityTypeConfiguration<EmployerDashboard>
{
    public void Configure(EntityTypeBuilder<EmployerDashboard> builder)
    {
        builder.ToTable("EmployerDashboards", AuditDbContext.Schema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("EmployerId").ValueGeneratedNever();
        builder.OwnsMany(d => d.Postings, postings =>
        {
            postings.ToTable("EmployerDashboardPostings", AuditDbContext.Schema);
            postings.WithOwner().HasForeignKey("EmployerId");
            postings.HasKey("EmployerId", nameof(DashboardPosting.JobPostingId));
            postings.Property(p => p.JobPostingId).ValueGeneratedNever();
            postings.Property(p => p.Title).HasMaxLength(300).IsRequired();
            postings.Property(p => p.Status).HasMaxLength(32).IsRequired();
        });
        builder.Navigation(d => d.Postings).HasField("_postings").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CandidateInsightConfiguration : IEntityTypeConfiguration<CandidateInsightRecord>
{
    public void Configure(EntityTypeBuilder<CandidateInsightRecord> builder)
    {
        builder.ToTable("CandidateInsights", AuditDbContext.Schema);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Ignore(i => i.WithheldFields);
        builder.Property(i => i.Availability).HasMaxLength(64);
        builder.Property(i => i.ExpectedSalary).HasPrecision(18, 2);
        builder.Property(i => i.FitScore).HasPrecision(9, 4);
        builder.Property(i => i.WithheldFieldsCsv).HasColumnName("WithheldFields").HasMaxLength(200).IsRequired();
        builder.HasIndex(i => new { i.JobPostingId, i.CandidateProfileId }).HasDatabaseName("IX_CandidateInsights_Job_Candidate");
    }
}

internal sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    private readonly bool _isSqlite;

    public ExportJobConfiguration(bool isSqlite) => _isSqlite = isSqlite;

    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("ExportJobs", AuditDbContext.Schema);
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(_isSqlite);
        builder.Ignore(j => j.IsInProgress);
        builder.Property(j => j.ReportType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(j => j.Format).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(j => j.ParametersHash).HasMaxLength(64).IsRequired();
        builder.Property(j => j.ParametersJson).IsRequired();
        builder.Property(j => j.ResultRef).HasMaxLength(500);
        builder.Property(j => j.FailureReason).HasMaxLength(500);

        // INV-04 final guard: at most one Queued/Generating job per administrator, report type and parameters.
        builder.HasIndex(j => new { j.RequestedBy, j.ReportType, j.ParametersHash }).IsUnique()
            .HasFilter("[Status] IN ('Queued','Generating')").HasDatabaseName("UQ_ExportJobs_InProgress");
    }
}
