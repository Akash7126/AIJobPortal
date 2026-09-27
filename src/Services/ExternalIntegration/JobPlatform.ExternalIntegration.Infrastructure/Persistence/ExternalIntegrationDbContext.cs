using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.ExternalIntegration.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence;

public sealed class ExternalIntegrationDbContext : BaseDbContext
{
    public const string Schema = "extint";

    public ExternalIntegrationDbContext(DbContextOptions<ExternalIntegrationDbContext> options) : base(options)
    {
    }

    public DbSet<ExternalJobSiteIntegration> Integrations => Set<ExternalJobSiteIntegration>();
    public DbSet<JobData> JobData => Set<JobData>();
    public DbSet<JobPostAttribution> JobPostAttributions => Set<JobPostAttribution>();
    public DbSet<JobDataMapping> JobDataMappings => Set<JobDataMapping>();
    public DbSet<ApiVersion> ApiVersions => Set<ApiVersion>();
    public DbSet<SoftwareInterfaceConnection> SoftwareInterfaces => Set<SoftwareInterfaceConnection>();
    public DbSet<PartnerCredential> PartnerCredentials => Set<PartnerCredential>();
    public DbSet<KnownPartnerAccount> KnownPartnerAccounts => Set<KnownPartnerAccount>();
    public DbSet<ApiSchemaAccessLog> ApiSchemaAccessLogs => Set<ApiSchemaAccessLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new ExternalJobSiteIntegrationConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SyncRunConfiguration());
        modelBuilder.ApplyConfiguration(new JobDataConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new JobPostAttributionConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new JobDataMappingConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ApiVersionConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SoftwareInterfaceConnectionConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new PartnerCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new KnownPartnerAccountConfiguration());
        modelBuilder.ApplyConfiguration(new ApiSchemaAccessLogConfiguration(IsSqlite));
        base.OnModelCreating(modelBuilder);
    }
}
