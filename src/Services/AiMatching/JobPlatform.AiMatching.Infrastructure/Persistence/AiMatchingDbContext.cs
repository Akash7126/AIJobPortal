using JobPlatform.AiMatching.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

/// <summary>Database of BC-10 (schema "matching", plus "messaging" from the building blocks). Nothing else reads or writes it.</summary>
public sealed class AiMatchingDbContext : BaseDbContext
{
    public const string Schema = "matching";

    private readonly IPiiProtector _pii;

    public AiMatchingDbContext(DbContextOptions<AiMatchingDbContext> options, IPiiProtector? pii = null) : base(options) => _pii = pii ?? NullPiiProtector.Instance;

    public DbSet<MatchingConfiguration> MatchingConfigurations => Set<MatchingConfiguration>();
    public DbSet<MatchScore> MatchScores => Set<MatchScore>();
    public DbSet<ResumeParsedData> ResumeParsedData => Set<ResumeParsedData>();
    public DbSet<SkillStandardization> SkillStandardizations => Set<SkillStandardization>();
    public DbSet<ParsedProfileData> ParsedProfileData => Set<ParsedProfileData>();
    public DbSet<JobSemantics> JobSemantics => Set<JobSemantics>();
    public DbSet<JobRecommendation> JobRecommendations => Set<JobRecommendation>();
    public DbSet<CandidateShortlist> CandidateShortlists => Set<CandidateShortlist>();
    public DbSet<KnownProfile> KnownProfiles => Set<KnownProfile>();
    public DbSet<KnownPosting> KnownPostings => Set<KnownPosting>();
    public DbSet<MatchingWorkItem> WorkItems => Set<MatchingWorkItem>();
    public DbSet<EmbeddingRecord> Embeddings => Set<EmbeddingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new MatchingConfigurationMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new ConfigurationHistoryMapping());
        modelBuilder.ApplyConfiguration(new ParsedFieldMapping(_pii));
        modelBuilder.ApplyConfiguration(new SkillMappingMapping());
        modelBuilder.ApplyConfiguration(new ProfileFieldMapping(_pii));
        modelBuilder.ApplyConfiguration(new MatchScoreMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new ResumeParsedDataMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new SkillStandardizationMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new ParsedProfileDataMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new JobSemanticsMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new JobRecommendationMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new CandidateShortlistMapping(IsSqlite));
        modelBuilder.ApplyConfiguration(new KnownProfileMapping());
        modelBuilder.ApplyConfiguration(new KnownPostingMapping());
        modelBuilder.ApplyConfiguration(new WorkItemMapping());
        modelBuilder.ApplyConfiguration(new EmbeddingMapping());
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>A stored embedding (vector as little-endian float32 bytes) with the model version that produced it.</summary>
public sealed class EmbeddingRecord
{
    public VectorKind EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public byte[] Vector { get; set; } = Array.Empty<byte>();
    public DateTime UpdatedAtUtc { get; set; }
}

public enum VectorKind
{
    Profile,
    Posting
}
