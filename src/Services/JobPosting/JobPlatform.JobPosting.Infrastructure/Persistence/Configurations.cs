using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobPosting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.JobPosting.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ValueConverter<IReadOnlyList<T>, string> ListConverter<T>() => new(
        v => JsonSerializer.Serialize(v, Options),
        s => JsonSerializer.Deserialize<List<T>>(s, Options) ?? new List<T>());

    public static ValueConverter<IReadOnlyDictionary<string, string>, string> DictConverter() => new(
        v => JsonSerializer.Serialize(v, Options),
        s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, Options) ?? new Dictionary<string, string>());

    public static ValueComparer<T> Comparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        v => JsonSerializer.Serialize(v, Options).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!);
}

internal sealed class JobPostingConfiguration(bool isSqlite) : IEntityTypeConfiguration<Domain.JobPosting>
{
    public void Configure(EntityTypeBuilder<Domain.JobPosting> builder)
    {
        builder.ToTable("JobPostings", JobPostingDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);

        builder.Property(p => p.EmployerAccountId);
        builder.Property(p => p.CategoryCode).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ContractType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.EducationLevelValue).HasConversion<string>().HasMaxLength(20).HasColumnName("EducationLevel");
        builder.Property(p => p.WorkFormat).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.JobLink).HasMaxLength(2000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.AdminSuspended).IsRequired();
        builder.Property(p => p.SuspendReason).HasMaxLength(500);
        builder.Property(p => p.TaxonomyVersion).IsRequired();
        builder.Property(p => p.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();

        builder.OwnsOne(p => p.Title, t =>
        {
            t.Property(x => x.Ar).HasColumnName("TitleAr").HasMaxLength(200).IsRequired();
            t.Property(x => x.En).HasColumnName("TitleEn").HasMaxLength(200).IsRequired();
        });
        builder.Navigation(p => p.Title).IsRequired();

        builder.OwnsOne(p => p.Summary, t =>
        {
            t.Property(x => x.Ar).HasColumnName("SummaryAr").HasMaxLength(5000).IsRequired();
            t.Property(x => x.En).HasColumnName("SummaryEn").HasMaxLength(5000).IsRequired();
        });
        builder.Navigation(p => p.Summary).IsRequired();

        builder.OwnsOne(p => p.Location, l =>
        {
            l.Property(x => x.Governorate).HasColumnName("Governorate").HasMaxLength(100);
            l.Property(x => x.City).HasColumnName("City").HasMaxLength(100);
        });

        builder.OwnsOne(p => p.Salary, s =>
        {
            s.Property(x => x.Min).HasColumnName("SalaryMin").HasColumnType("decimal(18,2)");
            s.Property(x => x.Max).HasColumnName("SalaryMax").HasColumnType("decimal(18,2)");
            s.Property(x => x.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(p => p.Deadline, d =>
        {
            d.Property(x => x.DateUtc).HasColumnName("DeadlineDate").IsRequired();
            d.Property(x => x.AutoClose).HasColumnName("AutoClose").IsRequired();
        });
        builder.Navigation(p => p.Deadline).IsRequired();

        builder.OwnsOne(p => p.Visibility, v =>
        {
            v.Property(x => x.Scope).HasColumnName("VisibilityScope").HasConversion<string>().HasMaxLength(20).IsRequired();
            v.Property(x => x.TargetJobSeekerIds).HasColumnName("VisibilityTargetJson")
                .HasConversion(Json.ListConverter<Guid>(), Json.Comparer<IReadOnlyList<Guid>>());
        });
        builder.Navigation(p => p.Visibility).IsRequired();

        builder.OwnsOne(p => p.Source, s =>
        {
            s.Property(x => x.Type).HasColumnName("SourceType").HasConversion<string>().HasMaxLength(20).IsRequired();
            s.Property(x => x.SourcePlatformId).HasColumnName("SourcePlatformId");
            s.Property(x => x.PlatformJobId).HasColumnName("SourcePlatformJobId").HasMaxLength(200);
            s.Property(x => x.SourceName).HasColumnName("SourceName").HasMaxLength(200);
            s.Property(x => x.BacklinkUrl).HasColumnName("SourceBacklinkUrl").HasMaxLength(2000);
            s.Property(x => x.AttributionPublic).HasColumnName("SourceAttributionPublic").IsRequired();
            s.HasIndex(x => x.PlatformJobId).HasDatabaseName("IX_JobPostings_Source_PlatformJobId");
        });
        builder.Navigation(p => p.Source).IsRequired();

        builder.Property(p => p.Skills).HasColumnName("SkillsJson")
            .HasConversion(Json.ListConverter<string>(), Json.Comparer<IReadOnlyList<string>>());
        builder.Property(p => p.RequiredTraining).HasColumnName("RequiredTrainingJson")
            .HasConversion(Json.ListConverter<string>(), Json.Comparer<IReadOnlyList<string>>());
        builder.Property(p => p.RequiredLanguages).HasColumnName("RequiredLanguagesJson")
            .HasConversion(Json.ListConverter<string>(), Json.Comparer<IReadOnlyList<string>>());
        builder.Property(p => p.OtherFields).HasColumnName("OtherFieldsJson")
            .HasConversion(Json.DictConverter(), Json.Comparer<IReadOnlyDictionary<string, string>>());

        // INV-03 (identical draft de-duplication) and Q-04 (import upsert) are enforced at the application layer (repository lookups before
        // Add), not by a DB constraint here: a hash collision across non-draft statuses must not block unrelated postings. See the status doc.
        builder.HasIndex(p => new { p.EmployerAccountId, p.ContentHash }).HasDatabaseName("IX_JobPostings_Employer_ContentHash");
        builder.HasIndex(p => p.Status).HasDatabaseName("IX_JobPostings_Status");
        builder.HasIndex(p => p.CategoryCode).HasDatabaseName("IX_JobPostings_Category");
    }
}

internal sealed class FavoriteJobListConfiguration(bool isSqlite) : IEntityTypeConfiguration<FavoriteJobList>
{
    public void Configure(EntityTypeBuilder<FavoriteJobList> builder)
    {
        builder.ToTable("FavoriteJobLists", JobPostingDbContext.Schema);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(f => f.OwnerAccountId).IsRequired();
        builder.Property(f => f.JobPostingIds).HasColumnName("JobPostingIdsJson")
            .HasConversion(Json.ListConverter<Guid>(), Json.Comparer<IReadOnlyList<Guid>>());
        builder.HasIndex(f => f.OwnerAccountId).IsUnique().HasDatabaseName("UQ_FavoriteJobLists_Owner");
    }
}

internal sealed class SavedSearchConfiguration(bool isSqlite) : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.ToTable("SavedSearches", JobPostingDbContext.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(s => s.OwnerAccountId).IsRequired();
        builder.Property(s => s.CriteriaHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.NotifyOnMatch).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.LastEvaluatedAtUtc);
        builder.Property(s => s.Criteria).HasColumnName("CriteriaJson")
            .HasConversion(new ValueConverter<SearchCriteria, string>(
                v => JsonSerializer.Serialize(ToDto(v), Json.Options),
                s2 => FromDto(JsonSerializer.Deserialize<CriteriaDto>(s2, Json.Options)!)),
                Json.Comparer<SearchCriteria>());
        builder.HasIndex(s => new { s.OwnerAccountId, s.CriteriaHash }).IsUnique().HasDatabaseName("UQ_SavedSearches_Owner_Hash");
        builder.HasIndex(s => s.NotifyOnMatch).HasDatabaseName("IX_SavedSearches_NotifyOnMatch");
    }

    private sealed record CriteriaDto(string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType,
        DateTime? PostedAfterUtc, DateTime? DeadlineBeforeUtc, string? CategoryCode);

    private static CriteriaDto ToDto(SearchCriteria c) => new(c.Keyword, c.Governorate, c.City, c.SalaryMin, c.SalaryMax, c.ContractType?.ToString(),
        c.PostedAfterUtc, c.DeadlineBeforeUtc, c.CategoryCode);

    private static SearchCriteria FromDto(CriteriaDto d) => new(d.Keyword, d.Governorate, d.City, d.SalaryMin, d.SalaryMax,
        d.ContractType is null ? null : Enum.Parse<ContractType>(d.ContractType, true), d.PostedAfterUtc, d.DeadlineBeforeUtc, d.CategoryCode);
}

internal sealed class InterestedListEntryConfiguration(bool isSqlite) : IEntityTypeConfiguration<InterestedListEntry>
{
    public void Configure(EntityTypeBuilder<InterestedListEntry> builder)
    {
        builder.ToTable("InterestedListEntries", JobPostingDbContext.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(e => e.OwnerAccountId).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.Reference).HasColumnName("ReferenceJson")
            .HasConversion(new ValueConverter<InterestedReference, string>(
                v => JsonSerializer.Serialize(ToDto(v), Json.Options),
                s => FromDto(JsonSerializer.Deserialize<ReferenceDto>(s, Json.Options)!)),
                Json.Comparer<InterestedReference>());
        builder.HasIndex(e => e.OwnerAccountId).HasDatabaseName("IX_InterestedListEntries_Owner");
    }

    private sealed record ReferenceDto(string Type, Guid? PostingId, string? Keyword, string? Governorate, string? City, decimal? SalaryMin,
        decimal? SalaryMax, string? ContractType, string? CategoryCode);

    private static ReferenceDto ToDto(InterestedReference r) => r.Type == InterestedReferenceType.Posting
        ? new ReferenceDto(r.Type.ToString(), r.PostingId, null, null, null, null, null, null, null)
        : new ReferenceDto(r.Type.ToString(), null, r.Criteria!.Keyword, r.Criteria.Governorate, r.Criteria.City, r.Criteria.SalaryMin,
            r.Criteria.SalaryMax, r.Criteria.ContractType?.ToString(), r.Criteria.CategoryCode);

    private static InterestedReference FromDto(ReferenceDto d) => Enum.Parse<InterestedReferenceType>(d.Type, true) == InterestedReferenceType.Posting
        ? InterestedReference.ToPosting(d.PostingId!.Value)
        : InterestedReference.ToFilter(new SearchCriteria(d.Keyword, d.Governorate, d.City, d.SalaryMin, d.SalaryMax,
            d.ContractType is null ? null : Enum.Parse<ContractType>(d.ContractType, true), null, null, d.CategoryCode));
}
