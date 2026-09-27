using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // Deserializes to List<string> (not string[]): CandidateInsight's WithheldFields backing field is a List<string>,
    // and EF assigns the converted value straight into whatever CLR type the backing field declares.
    public static ValueConverter<IReadOnlyList<string>, string> StringListConverter() => new(
        v => JsonSerializer.Serialize(v, Options),
        s => JsonSerializer.Deserialize<List<string>>(s, Options) ?? new List<string>());

    public static ValueComparer<IReadOnlyList<string>> StringListComparer() => new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        v => JsonSerializer.Serialize(v, Options).GetHashCode(),
        v => (IReadOnlyList<string>)(JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, Options), Options)!));
}

/// <summary>Mapping only (conversions, indexes, constraints). Business rules stay in the domain.</summary>
internal sealed class TalentPoolEntryConfiguration(bool isSqlite) : IEntityTypeConfiguration<TalentPoolEntry>
{
    public void Configure(EntityTypeBuilder<TalentPoolEntry> builder)
    {
        builder.ToTable("TalentPoolEntries", CandidateSourcingDbContext.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(e => e.Note).HasMaxLength(500);
        // INV-01: at most one *active* entry per (employer, candidate, posting); a re-add after Remove() gets a fresh row (Removed=false), which is
        // distinct from the old (Removed=true) row under this composite key, so it never collides with it.
        builder.HasIndex(e => new { e.EmployerAccountId, e.CandidateProfileId, e.JobPostingId, e.Removed })
            .IsUnique().HasDatabaseName("UQ_TalentPoolEntries_Employer_Candidate_Posting_Removed");
        builder.HasIndex(e => e.EmployerAccountId).HasDatabaseName("IX_TalentPoolEntries_Employer");
    }
}

internal sealed class QualificationThresholdConfiguration(bool isSqlite) : IEntityTypeConfiguration<QualificationThreshold>
{
    public void Configure(EntityTypeBuilder<QualificationThreshold> builder)
    {
        builder.ToTable("QualificationThresholds", CandidateSourcingDbContext.Schema);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.HasIndex(t => new { t.EmployerAccountId, t.JobPostingId }).IsUnique().HasDatabaseName("UQ_QualificationThresholds_Employer_Posting");
    }
}

internal sealed class CandidateInsightConfiguration(bool isSqlite) : IEntityTypeConfiguration<CandidateInsight>
{
    public void Configure(EntityTypeBuilder<CandidateInsight> builder)
    {
        builder.ToTable("CandidateInsights", CandidateSourcingDbContext.Schema);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(i => i.Availability).HasMaxLength(32);
        builder.Property(i => i.WithheldFields).HasColumnName("WithheldFieldsJson").IsRequired()
            .HasConversion(Json.StringListConverter(), Json.StringListComparer());
        builder.Property(i => i.Fit).HasColumnName("FitJson").IsRequired()
            .HasConversion(new ValueConverter<Fit, string>(
                v => JsonSerializer.Serialize(new FitDto(v.OverallScore, v.Criteria), Json.Options),
                s => ToFit(JsonSerializer.Deserialize<FitDto>(s, Json.Options)!)));
        builder.HasIndex(i => new { i.JobPostingId, i.CandidateProfileId }).HasDatabaseName("IX_CandidateInsights_Posting_Candidate");
    }

    private sealed record FitDto(decimal OverallScore, IReadOnlyList<FitCriterion> Criteria);

    private static Fit ToFit(FitDto dto) => Fit.Restore(dto.OverallScore, dto.Criteria);
}

internal sealed class CandidateProjectionConfiguration : IEntityTypeConfiguration<CandidateProjection>
{
    public void Configure(EntityTypeBuilder<CandidateProjection> builder)
    {
        builder.ToTable("CandidateProjections", CandidateSourcingDbContext.Schema);
        builder.HasKey(p => p.ProfileId);
        builder.Property(p => p.ProfileId).ValueGeneratedNever();
        builder.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.EducationLevel).HasMaxLength(32);
        builder.Property(p => p.LocationCode).HasMaxLength(32);
        builder.Property(p => p.Availability).HasMaxLength(32);
        builder.Property(p => p.Skills).HasColumnName("SkillsJson").IsRequired().HasConversion(Json.StringListConverter(), Json.StringListComparer());
        builder.HasIndex(p => p.OwnerAccountId).IsUnique().HasDatabaseName("UQ_CandidateProjections_OwnerAccount");
        builder.HasIndex(p => new { p.Visibility, p.Deactivated }).HasDatabaseName("IX_CandidateProjections_Visibility_Deactivated");
    }
}

internal sealed class VerifiedEmployerConfiguration : IEntityTypeConfiguration<VerifiedEmployer>
{
    public void Configure(EntityTypeBuilder<VerifiedEmployer> builder)
    {
        builder.ToTable("VerifiedEmployers", CandidateSourcingDbContext.Schema);
        builder.HasKey(e => e.EmployerAccountId);
        builder.Property(e => e.EmployerAccountId).ValueGeneratedNever();
    }
}
