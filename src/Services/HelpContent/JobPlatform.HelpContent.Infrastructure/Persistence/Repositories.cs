using JobPlatform.HelpContent.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence;

internal sealed class NewsArticleRepository(HelpContentDbContext db) : INewsArticleRepository
{
    public Task<NewsArticle?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.NewsArticles.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<NewsArticle?> GetDraftByContentHashAsync(string contentHash, CancellationToken ct = default) =>
        db.NewsArticles.FirstOrDefaultAsync(a => a.ContentHash == contentHash && a.Status == NewsStatus.Draft, ct);

    public void Add(NewsArticle article) => db.NewsArticles.Add(article);
}

internal sealed class ContentCategoryRepository(HelpContentDbContext db) : IContentCategoryRepository
{
    public Task<ContentCategory?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.ContentCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ContentCategory>> ListAsync(CancellationToken ct = default) => await db.ContentCategories.ToListAsync(ct);

    public void Add(ContentCategory category) => db.ContentCategories.Add(category);
}

internal sealed class ContentCategorizationRepository(HelpContentDbContext db) : IContentCategorizationRepository
{
    public Task<ContentCategorization?> GetByArticleAsync(Guid articleId, CancellationToken ct = default) =>
        db.ContentCategorizations.FirstOrDefaultAsync(c => c.ArticleId == articleId, ct);

    public void Add(ContentCategorization categorization) => db.ContentCategorizations.Add(categorization);
}

internal sealed class HelpContentRepository(HelpContentDbContext db) : IHelpContentRepository
{
    public Task<Domain.HelpContent?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.HelpContents.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(Domain.HelpContent content) => db.HelpContents.Add(content);
}

internal sealed class HelpTopicRepository(HelpContentDbContext db) : IHelpTopicRepository
{
    public Task<HelpTopic?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.HelpTopics.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<HelpTopic>> ListAsync(CancellationToken ct = default) => await db.HelpTopics.ToListAsync(ct);

    public void Add(HelpTopic topic) => db.HelpTopics.Add(topic);
}

internal sealed class HelpOrganizationRepository(HelpContentDbContext db) : IHelpOrganizationRepository
{
    public Task<HelpContentOrganization?> GetAsync(Guid helpContentId, CancellationToken ct = default) =>
        db.HelpContentOrganizations.FirstOrDefaultAsync(o => o.HelpContentId == helpContentId, ct);

    public void Add(HelpContentOrganization organization) => db.HelpContentOrganizations.Add(organization);
}

internal sealed class HelpFeedbackRepository(HelpContentDbContext db) : IHelpFeedbackRepository
{
    public Task<HelpFeedback?> GetByUserAndContentAsync(Guid userId, Guid helpContentId, CancellationToken ct = default) =>
        db.HelpFeedbacks.FirstOrDefaultAsync(f => f.UserId == userId && f.HelpContentId == helpContentId, ct);

    public void Add(HelpFeedback feedback) => db.HelpFeedbacks.Add(feedback);
}

internal sealed class TutorialProgressRepository(HelpContentDbContext db) : ITutorialProgressRepository
{
    public Task<TutorialProgress?> GetAsync(Guid userId, Guid tutorialId, CancellationToken ct = default) =>
        db.TutorialProgresses.FirstOrDefaultAsync(p => p.UserId == userId && p.TutorialId == tutorialId, ct);

    public void Add(TutorialProgress progress) => db.TutorialProgresses.Add(progress);
}

internal sealed class ContextHelpMappingRepository(HelpContentDbContext db) : IContextHelpMappingRepository
{
    public Task<ContextHelpMapping?> GetByPageKeyAsync(string pageKey, CancellationToken ct = default) =>
        db.ContextHelpMappings.FirstOrDefaultAsync(m => m.PageKey == pageKey, ct);

    public void Add(ContextHelpMapping mapping) => db.ContextHelpMappings.Add(mapping);
}

internal sealed class CompanyProfilePageRepository(HelpContentDbContext db) : ICompanyProfilePageRepository
{
    public Task<CompanyProfilePage?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        db.CompanyProfilePages.FirstOrDefaultAsync(p => p.EmployerAccountId == employerAccountId, ct);

    public void Add(CompanyProfilePage page) => db.CompanyProfilePages.Add(page);
}
