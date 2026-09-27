using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence;

public sealed class JobSeekerProfileDbContext : BaseDbContext
{
    public const string Schema = "profile";

    public JobSeekerProfileDbContext(DbContextOptions<JobSeekerProfileDbContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ProfileShareLink> ShareLinks => Set<ProfileShareLink>();
    public DbSet<SupplementaryDocument> Documents => Set<SupplementaryDocument>();
    public DbSet<JobPreference> JobPreferences => Set<JobPreference>();
    public DbSet<PrivacySetting> PrivacySettings => Set<PrivacySetting>();
    public DbSet<KnownAccount> KnownAccounts => Set<KnownAccount>();
    public DbSet<ProcessedParsedData> ProcessedParsedData => Set<ProcessedParsedData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new ProfileConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ResumeConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ProfileShareLinkConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SupplementaryDocumentConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new JobPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new PrivacySettingConfiguration());
        modelBuilder.ApplyConfiguration(new KnownAccountConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedParsedDataConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
