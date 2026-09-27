using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.HelpContent.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence;

public sealed class HelpContentDbContext : BaseDbContext
{
    public const string Schema = "content";

    public HelpContentDbContext(DbContextOptions<HelpContentDbContext> options) : base(options)
    {
    }

    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<ContentCategory> ContentCategories => Set<ContentCategory>();
    public DbSet<ContentCategorization> ContentCategorizations => Set<ContentCategorization>();
    public DbSet<Domain.HelpContent> HelpContents => Set<Domain.HelpContent>();
    public DbSet<HelpTopic> HelpTopics => Set<HelpTopic>();
    public DbSet<HelpContentOrganization> HelpContentOrganizations => Set<HelpContentOrganization>();
    public DbSet<HelpFeedback> HelpFeedbacks => Set<HelpFeedback>();
    public DbSet<TutorialProgress> TutorialProgresses => Set<TutorialProgress>();
    public DbSet<ContextHelpMapping> ContextHelpMappings => Set<ContextHelpMapping>();
    public DbSet<CompanyProfilePage> CompanyProfilePages => Set<CompanyProfilePage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new NewsArticleConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ContentCategoryConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ContentCategorizationConfiguration());
        modelBuilder.ApplyConfiguration(new HelpContentConfiguration());
        modelBuilder.ApplyConfiguration(new HelpTopicConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new HelpContentOrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new HelpFeedbackConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new TutorialProgressConfiguration(IsSqlite));
        modelBuilder.ApplyConfiguration(new ContextHelpMappingConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyProfilePageConfiguration(IsSqlite));
        base.OnModelCreating(modelBuilder);
    }
}
