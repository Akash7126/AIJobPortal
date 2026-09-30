using System.Text.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobPlatform.HelpContent.Infrastructure.Persistence;

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ValueConverter<IReadOnlyList<T>, string> ListConverter<T>() => new(
        v => JsonSerializer.Serialize(v, Options),
        s => JsonSerializer.Deserialize<List<T>>(s, Options) ?? new List<T>());

    public static ValueComparer<IReadOnlyList<T>> ListComparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        v => JsonSerializer.Serialize(v, Options).GetHashCode(),
        v => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(v, Options), Options)!);
}

/// <summary>Aggregate-column mapping for the two "later save wins" aggregates (ContentCategorization, HelpContentOrganization -
/// handover section 3.2/3.4 AC-03): the RowVersion column is kept for observability but is deliberately NOT a concurrency token, unlike
/// <see cref="AggregateMapping.ConfigureAggregate{TAggregate}"/>, so a concurrent overwrite never throws.</summary>
internal static class LastWriteWinsMapping
{
    public static void ConfigureAggregateLastWriteWins<TAggregate>(this EntityTypeBuilder<TAggregate> builder) where TAggregate : class, JobPlatform.SharedKernel.Domain.Interfaces.IAggregateRoot
    {
        builder.Property<byte[]>("RowVersion");
        builder.Property<long>("Version").HasColumnName("AggregateVersion");
        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.AggregateId);
    }
}

internal static class LocalizedTextMapping
{
    public static void OwnsLocalizedText<TOwner>(this EntityTypeBuilder<TOwner> builder, System.Linq.Expressions.Expression<Func<TOwner, LocalizedText?>> navigation,
        string arColumn, string enColumn, bool required = false) where TOwner : class
    {
        builder.OwnsOne(navigation, o =>
        {
            o.Property(x => x.Ar).HasColumnName(arColumn).HasMaxLength(50_000);
            o.Property(x => x.En).HasColumnName(enColumn).HasMaxLength(50_000);
        });
        if (required)
        {
            builder.Navigation(navigation).IsRequired();
        }
    }
}

internal sealed class NewsArticleConfiguration(bool isSqlite) : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.ToTable("NewsArticles", HelpContentDbContext.Schema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ContentHash).HasMaxLength(64).IsRequired();
        builder.OwnsLocalizedText(a => a.Title, "TitleAr", "TitleEn", required: true);
        builder.OwnsLocalizedText(a => a.Body, "BodyAr", "BodyEn", required: true);

        builder.OwnsMany(a => a.Media, m =>
        {
            m.ToTable("NewsMedia", HelpContentDbContext.Schema);
            m.WithOwner().HasForeignKey("NewsArticleId");
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).ValueGeneratedNever();
            m.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            m.Property(x => x.AltText).HasMaxLength(500);
            m.OwnsOne(x => x.File, f =>
            {
                f.Property(x => x.StorageKey).HasColumnName("StorageKey").HasMaxLength(500).IsRequired();
                f.Property(x => x.SizeBytes).HasColumnName("SizeBytes").IsRequired();
                f.Property(x => x.ContentType).HasColumnName("ContentType").HasMaxLength(100).IsRequired();
            });
            m.Navigation(x => x.File).IsRequired();
            m.HasIndex("NewsArticleId").HasDatabaseName("IX_NewsMedia_NewsArticleId");
        });
        builder.Navigation(a => a.Media).UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);

        // Draft de-duplication (INV-02) is enforced at the application layer (GetDraftByContentHashAsync before CreateDraft), not by a
        // filtered unique index here: SQLite (used by every non-Docker test in this repo) has no partial/filtered-index syntax EF can emit
        // portably across both providers, so the handover's "UQ(ContentHash) WHERE Status='Draft'" is approximated by this lookup instead.
        builder.HasIndex(a => a.ContentHash).HasDatabaseName("IX_NewsArticles_ContentHash");
        builder.HasIndex(a => new { a.Status, a.PublishedAtUtc }).HasDatabaseName("IX_NewsArticles_Status_PublishedAt");
    }
}

internal sealed class ContentCategoryConfiguration(bool isSqlite) : IEntityTypeConfiguration<ContentCategory>
{
    public void Configure(EntityTypeBuilder<ContentCategory> builder)
    {
        builder.ToTable("ContentCategories", HelpContentDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.OwnsLocalizedText(c => c.Name, "NameAr", "NameEn", required: true);
    }
}

/// <summary>"Later save wins" (see <see cref="LastWriteWinsMapping"/>): RowVersion is not a concurrency token.</summary>
internal sealed class ContentCategorizationConfiguration : IEntityTypeConfiguration<ContentCategorization>
{
    public void Configure(EntityTypeBuilder<ContentCategorization> builder)
    {
        builder.ToTable("ContentCategorizations", HelpContentDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregateLastWriteWins();
        builder.Property(c => c.ArticleId).IsRequired();
        builder.Property(c => c.CategoryIds).HasColumnName("CategoryIdsJson").HasConversion(Json.ListConverter<Guid>(), Json.ListComparer<Guid>());
        builder.Property(c => c.Tags).HasColumnName("TagsJson").HasConversion(Json.ListConverter<string>(), Json.ListComparer<string>());
        builder.HasIndex(c => c.ArticleId).IsUnique().HasDatabaseName("UQ_ContentCategorizations_ArticleId");
    }
}

internal sealed class HelpContentConfiguration : IEntityTypeConfiguration<Domain.HelpContent>
{
    public void Configure(EntityTypeBuilder<Domain.HelpContent> builder)
    {
        builder.ToTable("HelpContents", HelpContentDbContext.Schema);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.ConfigureAggregateLastWriteWins();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.CurrentVersion).IsRequired();

        builder.OwnsMany(c => c.Versions, v =>
        {
            v.ToTable("HelpContentVersions", HelpContentDbContext.Schema);
            v.WithOwner().HasForeignKey("HelpContentId");
            v.HasKey("HelpContentId", nameof(HelpContentVersion.Id));
            v.Property(x => x.Id).HasColumnName("VersionNo").ValueGeneratedNever();
            v.Ignore(x => x.VersionNo);
            v.Property(x => x.EditedBy).IsRequired();
            v.Property(x => x.EditedAtUtc).IsRequired();
            v.OwnsOne(x => x.Title, t =>
            {
                t.Property(x => x.Ar).HasColumnName("TitleAr").HasMaxLength(200);
                t.Property(x => x.En).HasColumnName("TitleEn").HasMaxLength(200);
            });
            v.OwnsOne(x => x.Body, b =>
            {
                b.Property(x => x.Ar).HasColumnName("BodyAr").HasMaxLength(50_000);
                b.Property(x => x.En).HasColumnName("BodyEn").HasMaxLength(50_000);
            });
        });
        builder.Navigation(c => c.Versions).UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);

        builder.OwnsMany(c => c.Media, m =>
        {
            m.ToTable("HelpMedia", HelpContentDbContext.Schema);
            m.WithOwner().HasForeignKey("HelpContentId");
            m.HasKey(x => x.Id);
            m.Property(x => x.Id).ValueGeneratedNever();
            m.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            m.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            m.Property(x => x.CaptionsRef).HasMaxLength(500);
            m.Property(x => x.TextAlternative).HasMaxLength(2000);
            m.HasIndex("HelpContentId").HasDatabaseName("IX_HelpMedia_HelpContentId");
        });
        builder.Navigation(c => c.Media).UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}

internal sealed class HelpTopicConfiguration(bool isSqlite) : IEntityTypeConfiguration<HelpTopic>
{
    public void Configure(EntityTypeBuilder<HelpTopic> builder)
    {
        builder.ToTable("HelpTopics", HelpContentDbContext.Schema);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.OwnsLocalizedText(t => t.Name, "NameAr", "NameEn", required: true);
    }
}

/// <summary>"Later save wins" (see <see cref="LastWriteWinsMapping"/>): RowVersion is not a concurrency token.</summary>
internal sealed class HelpContentOrganizationConfiguration : IEntityTypeConfiguration<HelpContentOrganization>
{
    public void Configure(EntityTypeBuilder<HelpContentOrganization> builder)
    {
        builder.ToTable("HelpContentOrganizations", HelpContentDbContext.Schema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.ConfigureAggregateLastWriteWins();
        builder.Property(o => o.HelpContentId).IsRequired();
        builder.Property(o => o.Roles).HasColumnName("RolesJson")
            .HasConversion(new ValueConverter<IReadOnlyList<HelpRole>, string>(
                v => JsonSerializer.Serialize(v.Select(r => r.ToString()), Json.Options),
                s => JsonSerializer.Deserialize<List<string>>(s, Json.Options)!.Select(Enum.Parse<HelpRole>).ToList()),
                new ValueComparer<IReadOnlyList<HelpRole>>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, r) => HashCode.Combine(h, r)), v => v.ToList()));
        builder.HasIndex(o => o.HelpContentId).IsUnique().HasDatabaseName("UQ_HelpContentOrganizations_HelpContentId");
    }
}

internal sealed class HelpFeedbackConfiguration(bool isSqlite) : IEntityTypeConfiguration<HelpFeedback>
{
    public void Configure(EntityTypeBuilder<HelpFeedback> builder)
    {
        builder.ToTable("HelpFeedback", HelpContentDbContext.Schema);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.Property(f => f.Rating).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Comment).HasMaxLength(HelpFeedback.MaxCommentLength);
        builder.HasIndex(f => new { f.HelpContentId, f.UserId }).IsUnique().HasDatabaseName("UQ_HelpFeedback_Content_User");
    }
}

internal sealed class TutorialProgressConfiguration(bool isSqlite) : IEntityTypeConfiguration<TutorialProgress>
{
    public void Configure(EntityTypeBuilder<TutorialProgress> builder)
    {
        builder.ToTable("TutorialProgress", HelpContentDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.HasIndex(p => new { p.UserId, p.TutorialId }).IsUnique().HasDatabaseName("UQ_TutorialProgress_User_Tutorial");
    }
}

/// <summary>"Later save wins" (see <see cref="LastWriteWinsMapping"/>): RowVersion is not a concurrency token.</summary>
internal sealed class ContextHelpMappingConfiguration : IEntityTypeConfiguration<ContextHelpMapping>
{
    public void Configure(EntityTypeBuilder<ContextHelpMapping> builder)
    {
        builder.ToTable("ContextHelpMappings", HelpContentDbContext.Schema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.ConfigureAggregateLastWriteWins();
        builder.Property(m => m.PageKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(m => m.PageKey).IsUnique().HasDatabaseName("UQ_ContextHelpMappings_PageKey");
    }
}

internal sealed class CompanyProfilePageConfiguration(bool isSqlite) : IEntityTypeConfiguration<CompanyProfilePage>
{
    public void Configure(EntityTypeBuilder<CompanyProfilePage> builder)
    {
        builder.ToTable("CompanyProfilePages", HelpContentDbContext.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.ConfigureAggregate(isSqlite);
        builder.OwnsLocalizedText(p => p.Background, "BackgroundAr", "BackgroundEn");
        builder.Property(p => p.Highlights).HasColumnName("HighlightsJson").HasConversion(Json.ListConverter<string>(), Json.ListComparer<string>());
        builder.HasIndex(p => p.EmployerAccountId).IsUnique().HasDatabaseName("UQ_CompanyProfilePages_EmployerAccountId");
    }
}
