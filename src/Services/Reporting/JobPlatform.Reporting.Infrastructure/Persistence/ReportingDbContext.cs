using JobPlatform.Reporting.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence;

/// <summary>Analytics store (schema "reporting": facts and rollups) plus report configuration (schema "config") of BC-12, in the BC's own database.</summary>
public sealed class ReportingDbContext : BaseDbContext
{
    public const string Schema = "reporting";
    public const string ConfigSchema = "config";

    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options)
    {
    }

    // analytics store
    public DbSet<FactEvent> FactEvents => Set<FactEvent>();
    public DbSet<FactJobPosting> FactJobPostings => Set<FactJobPosting>();
    public DbSet<FactSkillDemand> FactSkillDemand => Set<FactSkillDemand>();
    public DbSet<FactMatch> FactMatches => Set<FactMatch>();
    public DbSet<FactRegistration> FactRegistrations => Set<FactRegistration>();
    public DbSet<FactNotification> FactNotifications => Set<FactNotification>();
    public DbSet<FactSystemMetric> FactSystemMetrics => Set<FactSystemMetric>();
    public DbSet<FactOutcome> FactOutcomes => Set<FactOutcome>();
    public DbSet<AggDaily> AggDaily => Set<AggDaily>();

    // configuration
    public DbSet<ActivityLogRetentionPolicy> RetentionPolicies => Set<ActivityLogRetentionPolicy>();
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<SavedReport> SavedReports => Set<SavedReport>();
    public DbSet<ReportExport> ReportExports => Set<ReportExport>();
    public DbSet<ReportExportFile> ReportExportFiles => Set<ReportExportFile>();
    public DbSet<ReportAccessRule> ReportAccessRules => Set<ReportAccessRule>();
    public DbSet<ReportAccessDecision> ReportAccessDecisions => Set<ReportAccessDecision>();
    public DbSet<PerformanceAlertRule> PerformanceAlertRules => Set<PerformanceAlertRule>();
    public DbSet<PerformanceAlert> PerformanceAlerts => Set<PerformanceAlert>();
    public DbSet<LaborMarketReport> LaborMarketReports => Set<LaborMarketReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new FactEventConfiguration());
        modelBuilder.ApplyConfiguration(new FactJobPostingConfiguration());
        modelBuilder.ApplyConfiguration(new FactSkillDemandConfiguration());
        modelBuilder.ApplyConfiguration(new FactMatchConfiguration());
        modelBuilder.ApplyConfiguration(new FactRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new FactNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new FactSystemMetricConfiguration());
        modelBuilder.ApplyConfiguration(new FactOutcomeConfiguration());
        modelBuilder.ApplyConfiguration(new AggDailyConfiguration());
        modelBuilder.ApplyConfiguration(new RetentionPolicyConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ReportTemplateConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ReportScheduleConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SavedReportConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ReportExportConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ReportExportFileConfiguration());
        modelBuilder.ApplyConfiguration(new ReportAccessRuleConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ReportAccessDecisionConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new PerformanceAlertRuleConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new PerformanceAlertConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new LaborMarketReportConfiguration(IsSqlite));
        base.OnModelCreating(modelBuilder);
    }
}
