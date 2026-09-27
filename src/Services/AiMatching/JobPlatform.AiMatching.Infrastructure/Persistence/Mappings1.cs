using JobPlatform.AiMatching.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

// Mapping only (conversions, indexes, constraints). Business rules stay in the domain.

internal static class WeightsMapping
{
    public static void MapWeights<T>(this OwnedNavigationBuilder<T, CriterionWeights> weights) where T : class
    {
        weights.Property(w => w.SkillOverlap).HasColumnName("WeightSkillOverlap").HasPrecision(6, 2);
        weights.Property(w => w.Education).HasColumnName("WeightEducation").HasPrecision(6, 2);
        weights.Property(w => w.Training).HasColumnName("WeightTraining").HasPrecision(6, 2);
        weights.Property(w => w.Location).HasColumnName("WeightLocation").HasPrecision(6, 2);
        weights.Property(w => w.Experience).HasColumnName("WeightExperience").HasPrecision(6, 2);
        weights.Property(w => w.Salary).HasColumnName("WeightSalary").HasPrecision(6, 2);
    }
}

internal sealed class MatchingConfigurationMapping(bool isSqlite) : IEntityTypeConfiguration<MatchingConfiguration>
{
    public void Configure(EntityTypeBuilder<MatchingConfiguration> builder)
    {
        builder.ToTable("MatchingConfigurations", AiMatchingDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(c => c.MatchThresholdPercent).HasPrecision(6, 2);
        builder.Property(c => c.LowConfidenceThresholdPercent).HasPrecision(6, 2);
        builder.OwnsOne(c => c.Weights, w => w.MapWeights());
        builder.Navigation(c => c.Weights).IsRequired();
        builder.HasMany(c => c.History).WithOne().HasForeignKey("ConfigurationId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ConfigurationHistoryMapping : IEntityTypeConfiguration<ConfigurationHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ConfigurationHistoryEntry> builder)
    {
        builder.ToTable("ConfigurationHistory", AiMatchingDbContext.Schema);
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.MatchThresholdPercent).HasPrecision(6, 2);
        builder.Property(h => h.LowConfidenceThresholdPercent).HasPrecision(6, 2);
        builder.OwnsOne(h => h.Weights, w => w.MapWeights());
        builder.Navigation(h => h.Weights).IsRequired();
        builder.HasIndex(h => h.ConfigVersion).HasDatabaseName("IX_ConfigurationHistory_Version");
    }
}

internal sealed class MatchScoreMapping(bool isSqlite) : IEntityTypeConfiguration<MatchScore>
{
    public void Configure(EntityTypeBuilder<MatchScore> builder)
    {
        builder.ToTable("MatchScores", AiMatchingDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        var score = builder.Property(s => s.Score).HasPrecision(6, 2);
        if (isSqlite)
        {
            score.HasConversion<double>(); // SQLite cannot ORDER BY decimal; dev/tests store the (2-decimal) score as REAL
        }

        builder.Property(s => s.ModelVersion).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Breakdown).HasColumnName("BreakdownJson").HasJsonList().IsRequired();
        // Latest score per pair; the two indexes serve "best jobs of a seeker" and "best candidates of a posting" (score descending).
        builder.HasIndex(s => new { s.ProfileId, s.JobPostingId }).IsUnique().HasDatabaseName("UQ_MatchScores_Profile_Posting");
        builder.HasIndex(s => new { s.JobPostingId, s.Score }).IsDescending(false, true).HasDatabaseName("IX_MatchScores_Posting_Score");
        builder.HasIndex(s => new { s.ProfileId, s.Score }).IsDescending(false, true).HasDatabaseName("IX_MatchScores_Profile_Score");
    }
}

internal sealed class ResumeParsedDataMapping(bool isSqlite) : IEntityTypeConfiguration<ResumeParsedData>
{
    public void Configure(EntityTypeBuilder<ResumeParsedData> builder)
    {
        builder.ToTable("ResumeParsedData", AiMatchingDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Ignore(r => r.FieldsNeedingReview);
        builder.Property(r => r.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Language).HasConversion<string>().HasMaxLength(8);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.FailureCode).HasMaxLength(64);
        builder.Property(r => r.ModelVersion).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Skills).HasColumnName("SkillsJson").HasJsonList().IsRequired();
        builder.HasIndex(r => r.ProfileId).HasDatabaseName("IX_ResumeParsedData_Profile");
        builder.HasIndex(r => new { r.ResumeId, r.Sha256 }).IsUnique().HasDatabaseName("UQ_ResumeParsedData_Resume_Sha256");
        builder.HasMany(r => r.Fields).WithOne().HasForeignKey("ResumeParsedDataId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Fields).HasField("_fields").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ParsedFieldMapping(IPiiProtector pii) : IEntityTypeConfiguration<ParsedField>
{
    public void Configure(EntityTypeBuilder<ParsedField> builder)
    {
        builder.ToTable("ParsedFields", AiMatchingDbContext.Schema);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Name).HasConversion<string>().HasMaxLength(32);
        builder.Property(f => f.Value).Encrypted(pii).IsRequired();
        builder.Property(f => f.Confidence).HasPrecision(6, 2);
    }
}

internal sealed class SkillStandardizationMapping(bool isSqlite) : IEntityTypeConfiguration<SkillStandardization>
{
    public void Configure(EntityTypeBuilder<SkillStandardization> builder)
    {
        builder.ToTable("SkillStandardizations", AiMatchingDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(s => s.TaxonomyVersion).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(s => s.ResumeParsedDataId).HasDatabaseName("IX_SkillStandardizations_ResumeParsedData");
        builder.HasIndex(s => s.TaxonomyVersion).HasDatabaseName("IX_SkillStandardizations_TaxonomyVersion");
        builder.HasMany(s => s.Mappings).WithOne().HasForeignKey("SkillStandardizationId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Mappings).HasField("_mappings").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SkillMappingMapping : IEntityTypeConfiguration<SkillMapping>
{
    public void Configure(EntityTypeBuilder<SkillMapping> builder)
    {
        builder.ToTable("SkillMappings", AiMatchingDbContext.Schema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ExtractedTerm).HasMaxLength(200).IsRequired();
        builder.Property(m => m.CanonicalCode).HasMaxLength(64);
        builder.Property(m => m.Confidence).HasPrecision(6, 2);
    }
}
