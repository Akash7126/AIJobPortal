using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

internal sealed class ParsedProfileDataMapping(bool isSqlite) : IEntityTypeConfiguration<ParsedProfileData>
{
    public void Configure(EntityTypeBuilder<ParsedProfileData> builder)
    {
        builder.ToTable("ParsedProfileData", AiMatchingDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(p => p.ProfileId).IsUnique().HasDatabaseName("UQ_ParsedProfileData_Profile");
        builder.HasIndex(p => p.OwnerAccountId).HasDatabaseName("IX_ParsedProfileData_Owner");
        builder.HasMany(p => p.Fields).WithOne().HasForeignKey("ParsedProfileDataId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Fields).HasField("_fields").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProfileFieldMapping(IPiiProtector pii) : IEntityTypeConfiguration<ProfileField>
{
    public void Configure(EntityTypeBuilder<ProfileField> builder)
    {
        builder.ToTable("ProfileFields", AiMatchingDbContext.Schema);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Name).HasConversion<string>().HasMaxLength(32);
        builder.Property(f => f.Source).HasConversion<string>().HasMaxLength(16);
        builder.Property(f => f.Value).Encrypted(pii).IsRequired();
        builder.Property(f => f.Confidence).HasPrecision(6, 2);
    }
}

internal sealed class JobSemanticsMapping(bool isSqlite) : IEntityTypeConfiguration<JobSemantics>
{
    public void Configure(EntityTypeBuilder<JobSemantics> builder)
    {
        builder.ToTable("JobSemantics", AiMatchingDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("JobPostingId").ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(s => s.RequiredSkills).HasColumnName("SkillsJson").HasJsonList().IsRequired();
        builder.Property(s => s.ExperienceLevels).HasColumnName("LevelsJson").HasJsonList().IsRequired();
        builder.Property(s => s.Categories).HasColumnName("CategoriesJson").HasJsonList().IsRequired();
        builder.Property(s => s.Language).HasConversion<string>().HasMaxLength(8);
        builder.Property(s => s.Confidence).HasPrecision(6, 2);
        builder.Property(s => s.ModelVersion).HasMaxLength(64).IsRequired();
    }
}

internal sealed class JobRecommendationMapping(bool isSqlite) : IEntityTypeConfiguration<JobRecommendation>
{
    public void Configure(EntityTypeBuilder<JobRecommendation> builder)
    {
        builder.ToTable("JobRecommendations", AiMatchingDbContext.Schema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(r => r.Items).HasColumnName("ItemsJson").HasJsonList().IsRequired();
        builder.Property(r => r.Strategy).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(r => new { r.ProfileId, r.ComputedAtUtc }).IsDescending(false, true).HasDatabaseName("IX_JobRecommendations_Profile_ComputedAt");
    }
}

internal sealed class CandidateShortlistMapping(bool isSqlite) : IEntityTypeConfiguration<CandidateShortlist>
{
    public void Configure(EntityTypeBuilder<CandidateShortlist> builder)
    {
        builder.ToTable("CandidateShortlists", AiMatchingDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(s => s.Items).HasColumnName("ItemsJson").HasJsonList().IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.FailureReason).HasMaxLength(500);
        builder.HasIndex(s => s.JobPostingId).HasDatabaseName("IX_CandidateShortlists_Posting");
    }
}

internal sealed class KnownProfileMapping : IEntityTypeConfiguration<KnownProfile>
{
    public void Configure(EntityTypeBuilder<KnownProfile> builder)
    {
        builder.ToTable("KnownProfiles", AiMatchingDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ProfileId").ValueGeneratedNever();
        builder.Property(p => p.Standing).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(p => p.OwnerAccountId).IsUnique().HasDatabaseName("UQ_KnownProfiles_Owner");
    }
}

internal sealed class KnownPostingMapping : IEntityTypeConfiguration<KnownPosting>
{
    public void Configure(EntityTypeBuilder<KnownPosting> builder)
    {
        builder.ToTable("KnownPostings", AiMatchingDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("JobPostingId").ValueGeneratedNever();
        builder.Ignore(p => p.IsActive);
        builder.Property(p => p.Status).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(300).IsRequired();
        builder.HasIndex(p => new { p.EmployerAccountId, p.Status }).HasDatabaseName("IX_KnownPostings_Employer_Status");
    }
}

internal sealed class WorkItemMapping : IEntityTypeConfiguration<MatchingWorkItem>
{
    public void Configure(EntityTypeBuilder<MatchingWorkItem> builder)
    {
        builder.ToTable("WorkItems", AiMatchingDbContext.Schema);
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(w => w.LastError).HasMaxLength(1000);
        builder.HasIndex(w => new { w.Status, w.NextAttemptUtc }).HasDatabaseName("IX_WorkItems_Status_NextAttempt");
        // At most one pending item per (kind, entity): the final guard of the idempotent enqueue.
        builder.HasIndex(w => new { w.Kind, w.EntityId }).IsUnique().HasFilter("[Status] = 'Pending'").HasDatabaseName("UQ_WorkItems_Pending");
    }
}

internal sealed class EmbeddingMapping : IEntityTypeConfiguration<EmbeddingRecord>
{
    public void Configure(EntityTypeBuilder<EmbeddingRecord> builder)
    {
        builder.ToTable("Embeddings", AiMatchingDbContext.Schema);
        builder.HasKey(e => new { e.EntityType, e.EntityId });
        builder.Property(e => e.EntityType).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.ModelVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Vector).IsRequired();
    }
}
