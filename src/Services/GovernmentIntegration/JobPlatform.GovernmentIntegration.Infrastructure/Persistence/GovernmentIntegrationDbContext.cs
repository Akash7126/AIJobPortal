using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.GovernmentIntegration.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence;

public sealed class GovernmentIntegrationDbContext : BaseDbContext
{
    public const string Schema = "govint";

    public GovernmentIntegrationDbContext(DbContextOptions<GovernmentIntegrationDbContext> options) : base(options)
    {
    }

    public DbSet<EmployerVerification> EmployerVerifications => Set<EmployerVerification>();
    public DbSet<GovernmentVerificationData> GovernmentVerificationData => Set<GovernmentVerificationData>();
    public DbSet<EducationalCredentialVerification> EducationalCredentialVerifications => Set<EducationalCredentialVerification>();
    public DbSet<IdentityVerificationData> IdentityVerifications => Set<IdentityVerificationData>();
    public DbSet<LegacyData> LegacyData => Set<LegacyData>();
    public DbSet<DataQuality> DataQuality => Set<DataQuality>();
    public DbSet<MigrationRun> MigrationRuns => Set<MigrationRun>();
    public DbSet<GovernmentSourceConnection> GovernmentSourceConnections => Set<GovernmentSourceConnection>();
    public DbSet<KnownAccount> KnownAccounts => Set<KnownAccount>();
    public DbSet<GovernmentDataAccessLogEntry> GovernmentDataAccessLog => Set<GovernmentDataAccessLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new EmployerVerificationConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new VerificationAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new GovernmentVerificationDataConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new EducationalCredentialVerificationConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new IdentityVerificationDataConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new LegacyDataConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new DataQualityConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new MigrationRunConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new MigrationPhaseConfiguration());
        modelBuilder.ApplyConfiguration(new MigrationLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new GovernmentSourceConnectionConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new KnownAccountConfiguration());
        modelBuilder.ApplyConfiguration(new GovernmentDataAccessLogEntryConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
