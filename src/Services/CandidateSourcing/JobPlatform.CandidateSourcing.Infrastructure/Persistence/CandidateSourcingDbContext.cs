using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence;

public sealed class CandidateSourcingDbContext : BaseDbContext
{
    public const string Schema = "sourcing";

    public CandidateSourcingDbContext(DbContextOptions<CandidateSourcingDbContext> options) : base(options)
    {
    }

    public DbSet<TalentPoolEntry> TalentPoolEntries => Set<TalentPoolEntry>();
    public DbSet<QualificationThreshold> QualificationThresholds => Set<QualificationThreshold>();
    public DbSet<CandidateInsight> CandidateInsights => Set<CandidateInsight>();
    public DbSet<CandidateProjection> CandidateProjections => Set<CandidateProjection>();
    public DbSet<VerifiedEmployer> VerifiedEmployers => Set<VerifiedEmployer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new TalentPoolEntryConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new QualificationThresholdConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new CandidateInsightConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new CandidateProjectionConfiguration());
        modelBuilder.ApplyConfiguration(new VerifiedEmployerConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
