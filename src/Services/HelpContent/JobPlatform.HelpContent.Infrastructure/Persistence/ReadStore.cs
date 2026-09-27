using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence;

/// <summary>
/// Read side (foundation section 3.5). Category/tag and topic/role filters are resolved by loading the small ContentCategorization /
/// HelpContentOrganization side-tables into memory rather than a SQL predicate against their JSON-converted columns (portable across SQL
/// Server and SQLite at the seed volumes this repo tests at). Keyword search uses a portable LIKE predicate rather than the handover's
/// SQL Server full-text index (same documented trade-off as BC-09's JobPostingSearchReadModel) - see the BC-06 status doc.
/// </summary>
internal sealed class HelpContentReadStore(HelpContentDbContext db, IMediaStorage storage) : IHelpContentReadStore
{
    private const string Uncategorized = "Uncategorized";

    public async Task<NewsArticleView?> GetNewsArticleAsync(Guid id, CancellationToken ct = default)
    {
        var article = await db.NewsArticles.AsNoTracking().Include(a => a.Media).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null)
        {
            return null;
        }

        var categorization = await db.ContentCategorizations.AsNoTracking().FirstOrDefaultAsync(c => c.ArticleId == id, ct);
        return ToView(article, categorization);
    }

    public async Task<PagedResult<NewsListItemView>> ListNewsAsync(Guid? categoryId, string? status, PageRequest page, CancellationToken ct = default)
    {
        var query = db.NewsArticles.AsNoTracking().AsQueryable();
        if (status is { Length: > 0 } && Enum.TryParse<NewsStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        var categorizations = await db.ContentCategorizations.AsNoTracking().ToListAsync(ct);
        var categories = await db.ContentCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        if (categoryId is { } cid)
        {
            var matching = categorizations.Where(c => c.CategoryIds.Contains(cid)).Select(c => c.ArticleId).ToHashSet();
            query = query.Where(a => matching.Contains(a.Id));
        }

        query = query.OrderByDescending(a => a.PublishedAtUtc ?? a.CreatedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<NewsListItemView>(items.Select(a => ToListItem(a, categorizations, categories)).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<NewsListItemView>> SearchNewsArchiveAsync(string? keyword, PageRequest page, CancellationToken ct = default)
    {
        var query = db.NewsArticles.AsNoTracking().Where(a => a.Status == NewsStatus.Archived);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";
            query = query.Where(a => EF.Functions.Like(a.Title.En!, pattern) || EF.Functions.Like(a.Title.Ar!, pattern)
                || EF.Functions.Like(a.Body.En!, pattern) || EF.Functions.Like(a.Body.Ar!, pattern));
        }

        query = query.OrderByDescending(a => a.ArchivedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        var categorizations = await db.ContentCategorizations.AsNoTracking().ToListAsync(ct);
        var categories = await db.ContentCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        return new PagedResult<NewsListItemView>(items.Select(a => ToListItem(a, categorizations, categories)).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<NewsListItemView>> GetGeneralFeedAsync(int take, CancellationToken ct = default)
    {
        var items = await db.NewsArticles.AsNoTracking().Where(a => a.Status == NewsStatus.Published)
            .OrderByDescending(a => a.PublishedAtUtc).Take(take).ToListAsync(ct);
        var categorizations = await db.ContentCategorizations.AsNoTracking().ToListAsync(ct);
        var categories = await db.ContentCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        return items.Select(a => ToListItem(a, categorizations, categories)).ToArray();
    }

    public async Task<IReadOnlyList<NewsListItemView>> GetPersonalizedFeedAsync(IReadOnlyList<string> interestTags, int take, CancellationToken ct = default)
    {
        var categorizations = await db.ContentCategorizations.AsNoTracking().ToListAsync(ct);
        var categories = await db.ContentCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var matchingArticleIds = categorizations
            .Where(c => c.Tags.Any(t => interestTags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Select(c => c.ArticleId).ToHashSet();
        if (matchingArticleIds.Count == 0)
        {
            return Array.Empty<NewsListItemView>();
        }

        var items = await db.NewsArticles.AsNoTracking()
            .Where(a => a.Status == NewsStatus.Published && matchingArticleIds.Contains(a.Id))
            .OrderByDescending(a => a.PublishedAtUtc).Take(take).ToListAsync(ct);
        return items.Select(a => ToListItem(a, categorizations, categories)).ToArray();
    }

    public async Task<IReadOnlyList<ContentCategoryView>> ListCategoriesAsync(CancellationToken ct = default) =>
        await db.ContentCategories.AsNoTracking()
            .Select(c => new ContentCategoryView(c.Id, new LocalizedView(c.Name.Ar, c.Name.En), c.IsDeleted))
            .ToListAsync(ct);

    public async Task<HelpContentView?> GetHelpContentAsync(Guid id, CancellationToken ct = default)
    {
        var content = await db.HelpContents.AsNoTracking().Include(c => c.Versions).Include(c => c.Media).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (content is null)
        {
            return null;
        }

        var organization = await db.HelpContentOrganizations.AsNoTracking().FirstOrDefaultAsync(o => o.HelpContentId == id, ct);
        return ToHelpView(content, organization);
    }

    public async Task<IReadOnlyList<HelpCenterTopicView>> GetHelpCenterAsync(HelpRole? role, CancellationToken ct = default)
    {
        var organizations = await db.HelpContentOrganizations.AsNoTracking().ToListAsync(ct);
        var content = await db.HelpContents.AsNoTracking().Include(c => c.Versions).ToListAsync(ct);
        var topics = await db.HelpTopics.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);

        var visible = content.Where(c => IsVisibleToRole(organizations, c.Id, role)).ToList();
        return visible.GroupBy(c => organizations.FirstOrDefault(o => o.HelpContentId == c.Id)?.TopicId)
            .Select(g =>
            {
                var topicName = TopicName(topics, g.Key);
                return new HelpCenterTopicView(g.Key, topicName,
                    g.Select(c => new HelpSearchResultView(c.Id, c.Kind.ToString(), new LocalizedView(c.Current.Title.Ar, c.Current.Title.En), topicName)).ToArray());
            }).ToArray();
    }

    public async Task<PagedResult<HelpSearchResultView>> SearchHelpContentAsync(string keyword, HelpRole? role, PageRequest page, CancellationToken ct = default)
    {
        var organizations = await db.HelpContentOrganizations.AsNoTracking().ToListAsync(ct);
        var all = await db.HelpContents.AsNoTracking().Include(c => c.Versions).ToListAsync(ct);
        var topics = await db.HelpTopics.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);

        var matches = all.Where(c => IsVisibleToRole(organizations, c.Id, role)
            && (Matches(c.Current.Title.Ar, keyword) || Matches(c.Current.Title.En, keyword) || Matches(c.Current.Body.Ar, keyword)
                || Matches(c.Current.Body.En, keyword)))
            .ToList();

        var total = matches.Count;
        var pageItems = matches.Skip(page.Skip).Take(page.PageSize)
            .Select(c =>
            {
                var topicId = organizations.FirstOrDefault(o => o.HelpContentId == c.Id)?.TopicId;
                return new HelpSearchResultView(c.Id, c.Kind.ToString(), new LocalizedView(c.Current.Title.Ar, c.Current.Title.En), TopicName(topics, topicId));
            }).ToArray();
        return new PagedResult<HelpSearchResultView>(pageItems, page.Page, page.PageSize, total);
    }

    /// <summary>Content with no organization row yet, or an empty role list, is visible to everyone (handover section 3.4: role assignment
    /// narrows visibility, it never blocks unorganised content). A specific role also matches content assigned to it.</summary>
    private static bool IsVisibleToRole(IReadOnlyList<HelpContentOrganization> organizations, Guid helpContentId, HelpRole? role)
    {
        if (role is null)
        {
            return true;
        }

        var organization = organizations.FirstOrDefault(o => o.HelpContentId == helpContentId);
        return organization is null || organization.Roles.Count == 0 || organization.Roles.Contains(role.Value);
    }

    private static string TopicName(IReadOnlyDictionary<Guid, HelpTopic> topics, Guid? topicId) =>
        topicId is { } id && topics.TryGetValue(id, out var topic) && !topic.IsRemoved ? topic.Name.En ?? topic.Name.Ar ?? Uncategorized : Uncategorized;

    public async Task<IReadOnlyList<HelpTopicView>> ListHelpTopicsAsync(CancellationToken ct = default) =>
        await db.HelpTopics.AsNoTracking().Select(t => new HelpTopicView(t.Id, new LocalizedView(t.Name.Ar, t.Name.En), t.IsRemoved)).ToListAsync(ct);

    public async Task<HelpFeedbackSummaryView?> GetFeedbackSummaryAsync(Guid helpContentId, CancellationToken ct = default)
    {
        var feedback = await db.HelpFeedbacks.AsNoTracking().Where(f => f.HelpContentId == helpContentId).ToListAsync(ct);
        return new HelpFeedbackSummaryView(helpContentId, feedback.Count(f => f.Rating == FeedbackRating.Helpful),
            feedback.Count(f => f.Rating == FeedbackRating.NotHelpful));
    }

    private static bool Matches(string? text, string keyword) => text is not null && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private NewsArticleView ToView(NewsArticle a, ContentCategorization? categorization) => new(
        a.Id, a.Kind.ToString(), new LocalizedView(a.Title.Ar, a.Title.En), new LocalizedView(a.Body.Ar, a.Body.En), a.Status.ToString(),
        a.Media.Select(m => new NewsMediaView(m.Id, m.Type.ToString(), storage.UrlFor(m.File.StorageKey), m.AltText)).ToArray(),
        categorization?.CategoryIds ?? Array.Empty<Guid>(), categorization?.Tags ?? Array.Empty<string>(), a.CreatedBy, a.CreatedAtUtc, a.PublishedAtUtc,
        a.ArchivedAtUtc, a.RowVersion);

    private static NewsListItemView ToListItem(NewsArticle a, IReadOnlyList<ContentCategorization> categorizations, IReadOnlyDictionary<Guid, ContentCategory> categories)
    {
        var categorization = categorizations.FirstOrDefault(c => c.ArticleId == a.Id);
        var names = categorization is null
            ? Array.Empty<string>()
            : categorization.CategoryIds.Select(id => categories.TryGetValue(id, out var cat) && !cat.IsDeleted ? cat.Name.En ?? cat.Name.Ar ?? Uncategorized : Uncategorized).ToArray();
        return new NewsListItemView(a.Id, a.Kind.ToString(), new LocalizedView(a.Title.Ar, a.Title.En), a.Status.ToString(),
            names.Length == 0 ? new[] { Uncategorized } : names, a.PublishedAtUtc);
    }

    private HelpContentView ToHelpView(Domain.HelpContent c, HelpContentOrganization? organization) => new(
        c.Id, c.Kind.ToString(), new LocalizedView(c.Current.Title.Ar, c.Current.Title.En), new LocalizedView(c.Current.Body.Ar, c.Current.Body.En),
        c.CurrentVersion, organization?.TopicId, (organization?.Roles ?? Array.Empty<HelpRole>()).Select(r => r.ToString()).ToArray(),
        c.Media.Select(m => new HelpMediaView(m.Id, m.Type.ToString(), storage.UrlFor(m.StorageKey), m.CaptionsRef, m.TextAlternative)).ToArray(), c.RowVersion);
}
