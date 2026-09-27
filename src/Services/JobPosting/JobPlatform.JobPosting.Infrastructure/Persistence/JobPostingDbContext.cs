using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobPosting.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence;

public sealed class JobPostingDbContext : BaseDbContext
{
    public const string Schema = "jobs";

    public JobPostingDbContext(DbContextOptions<JobPostingDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.JobPosting> JobPostings => Set<Domain.JobPosting>();
    public DbSet<FavoriteJobList> FavoriteJobLists => Set<FavoriteJobList>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<InterestedListEntry> InterestedListEntries => Set<InterestedListEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new JobPostingConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new FavoriteJobListConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new SavedSearchConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new InterestedListEntryConfiguration(IsSqlite));
        base.OnModelCreating(modelBuilder);
    }
}
