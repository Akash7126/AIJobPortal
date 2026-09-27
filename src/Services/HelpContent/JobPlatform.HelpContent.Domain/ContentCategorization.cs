using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

/// <summary>AGG-31 catalogue entry: a reusable category label. Soft-deleted rather than removed so already-assigned articles never dangle
/// (handover section 3.2: a deleted category's articles show as "uncategorized").</summary>
public sealed class ContentCategory : AggregateRoot<Guid>
{
    private ContentCategory()
    {
    }

    public LocalizedText Name { get; private set; } = default!;

    public bool IsDeleted { get; private set; }

    public static ContentCategory Create(Guid id, LocalizedText name, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.CategorizationAdminOnly, ErrorCodes.NewsForbidden));
        Check(new BusinessRule(RuleCodes.NewsRequiredField, "The category name is required.", name.IsEmpty, ErrorCodes.NewsRequiredField));
        return new ContentCategory { Id = id, Name = name };
    }

    public void SoftDelete(Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.CategorizationAdminOnly, ErrorCodes.NewsForbidden));
        IsDeleted = true;
    }
}

/// <summary>
/// AGG-31: the categories and tags assigned to one news article (1:1 per article, handover section 3.2). Concurrent edits are resolved
/// "later save wins" (story AC-03: no RowVersion rejection) - the Infrastructure mapping deliberately does not treat RowVersion as a
/// concurrency token for this aggregate, unlike NewsArticle itself.
/// </summary>
public sealed class ContentCategorization : AggregateRoot<Guid>
{
    private ContentCategorization()
    {
    }

    public Guid ArticleId { get; private set; }

    public IReadOnlyList<Guid> CategoryIds { get; private set; } = new List<Guid>();

    public IReadOnlyList<string> Tags { get; private set; } = new List<string>();

    public bool HasAnyCategoryOrTag => CategoryIds.Count > 0 || Tags.Count > 0;

    /// <summary>Id equals ArticleId: this aggregate is a 1:1 side-table of NewsArticle, never merged into it (different concurrency
    /// semantics - see the class remarks).</summary>
    public static ContentCategorization CreateFor(Guid articleId) => new() { Id = articleId, ArticleId = articleId };

    public void Assign(IReadOnlyList<Guid> categoryIds, IReadOnlyList<string> tags, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.CategorizationAdminOnly, ErrorCodes.NewsForbidden));
        CategoryIds = categoryIds.Distinct().ToList();
        Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
