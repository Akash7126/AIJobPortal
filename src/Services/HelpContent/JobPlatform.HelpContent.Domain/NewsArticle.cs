using System.Security.Cryptography;
using System.Text;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.HelpContent.Domain;

public enum NewsKind
{
    Article,
    Announcement
}

public enum NewsStatus
{
    Draft,
    Published,
    Archived
}

public enum NewsMediaType
{
    Image,
    Embed
}

/// <summary>Uploaded file reference for a news article's media item. Never the bytes themselves - those live behind Application.Interfaces.IMediaStorage.</summary>
public sealed class NewsMediaFile : ValueObject
{
    public NewsMediaFile(string storageKey, long sizeBytes, string contentType)
    {
        StorageKey = storageKey;
        SizeBytes = sizeBytes;
        ContentType = contentType;
    }

    public string StorageKey { get; }
    public long SizeBytes { get; }
    public string ContentType { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StorageKey;
        yield return SizeBytes;
        yield return ContentType;
    }
}

/// <summary>An image or embedded-media item on a news article (handover section 3.1). Alt text is required before the article can be
/// published (THR-067) but may be added after the media itself is attached.</summary>
public sealed class NewsMediaItem : Entity<Guid>
{
    private NewsMediaItem()
    {
    }

    internal NewsMediaItem(Guid id, NewsMediaType type, NewsMediaFile file, string? altText)
    {
        Id = id;
        Type = type;
        File = file;
        AltText = altText;
    }

    public NewsMediaType Type { get; private set; }

    public NewsMediaFile File { get; private set; } = default!;

    public string? AltText { get; private set; }

    internal void SetAltText(string altText) => AltText = altText;
}

/// <summary>
/// AGG-30: an administrator-authored news article or announcement (handover section 3.1). Lifecycle Draft -&gt; Published -&gt; Archived;
/// publishing and archiving twice are no-ops (idempotent, no duplicate event - stories 3.7.1-01/-06 AC-02).
/// </summary>
public sealed class NewsArticle : AggregateRoot<Guid>
{
    public const long MaxMediaSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp" };

    private readonly List<NewsMediaItem> _media = new();

    private NewsArticle()
    {
    }

    public NewsKind Kind { get; private set; }

    public LocalizedText Title { get; private set; } = default!;

    public LocalizedText Body { get; private set; } = default!;

    public NewsStatus Status { get; private set; }

    /// <summary>Normalised title+body hash used for draft de-duplication (INV-02, handover section 8: UQ(ContentHash) WHERE Status='Draft').</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public IReadOnlyCollection<NewsMediaItem> Media => _media;

    /// <summary>Normalises title+body (trim, invariant lowercase) into a stable SHA-256 hash for draft de-duplication.</summary>
    public static string ComputeContentHash(LocalizedText title, LocalizedText body)
    {
        var normalized = string.Join('|', title.Ar, title.En, body.Ar, body.En)
            .Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>INV-01 title and body required; INV-07 admin-only. The caller (application layer) is responsible for INV-02: looking up an
    /// existing draft by content hash first and calling <see cref="EditDraft"/> on it instead of creating a duplicate.</summary>
    public static NewsArticle CreateDraft(Guid id, NewsKind kind, LocalizedText title, LocalizedText body, Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.NewsAdminOnly, ErrorCodes.NewsForbidden));
        Check(RequiredFieldsRule(title, body));

        var article = new NewsArticle
        {
            Id = id,
            Kind = kind,
            Title = title,
            Body = body,
            Status = NewsStatus.Draft,
            ContentHash = ComputeContentHash(title, body),
            CreatedBy = actor.Id,
            CreatedAtUtc = nowUtc
        };
        article.Raise(new NewsArticleCreatedDomainEvent(id, actor.Id, kind.ToString(), nowUtc));
        return article;
    }

    /// <summary>Edits a draft in place (INV-02: an identical resubmission and a genuine edit both land here). Only while still a draft.</summary>
    public void EditDraft(LocalizedText title, LocalizedText body, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.NewsAdminOnly, ErrorCodes.NewsForbidden));
        Check(new BusinessRule(RuleCodes.NewsEditOnlyDraft, "Only a draft article can be edited.", Status != NewsStatus.Draft,
            kind: BusinessRuleKind.Conflict));
        Check(RequiredFieldsRule(title, body));

        Title = title;
        Body = body;
        ContentHash = ComputeContentHash(title, body);
    }

    /// <summary>INV-04 size and format rules; alt text is optional at attach time but required before publish (THR-067).</summary>
    public NewsMediaItem AddMedia(Guid mediaId, NewsMediaType type, NewsMediaFile file, string? altText, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.NewsAdminOnly, ErrorCodes.NewsForbidden));
        Check(new BusinessRule(RuleCodes.NewsMediaTooLarge, "The file exceeds the 10 MB limit.", file.SizeBytes > MaxMediaSizeBytes,
            ErrorCodes.NewsTooLarge, BusinessRuleKind.BusinessRule));
        var supported = type == NewsMediaType.Image && ImageContentTypes.Contains(file.ContentType);
        Check(new BusinessRule(RuleCodes.NewsMediaUnsupportedFormat, "The media type or format is not supported.", !supported && type == NewsMediaType.Image,
            ErrorCodes.NewsUnsupportedFormat, BusinessRuleKind.BusinessRule));

        var item = new NewsMediaItem(mediaId, type, file, altText);
        _media.Add(item);
        return item;
    }

    /// <summary>INV-03 must currently be Draft; publishing an already-published article is a no-op (no duplicate event, story AC-02).
    /// CategoryIds is data owned by the sibling ContentCategorization aggregate, supplied by the caller purely for the event payload
    /// (handover section 5.1 "+proposed categoryIds[]") - NewsArticle itself has no dependency on that aggregate.</summary>
    public void Publish(Actor actor, DateTime nowUtc, IReadOnlyList<Guid> categoryIds)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.NewsAdminOnly, ErrorCodes.NewsForbidden));
        if (Status == NewsStatus.Published)
        {
            return;
        }

        Check(new BusinessRule(RuleCodes.NewsMustBeDraftToPublish, "Only a draft article can be published.", Status != NewsStatus.Draft,
            kind: BusinessRuleKind.Conflict));
        Check(new BusinessRule(RuleCodes.NewsMediaNoAltText, "Every image requires alt text before the article can be published.",
            _media.Any(m => m.Type == NewsMediaType.Image && string.IsNullOrWhiteSpace(m.AltText))));

        Status = NewsStatus.Published;
        PublishedAtUtc = nowUtc;
        Raise(new NewsArticlePublishedDomainEvent(Id, actor.Id, categoryIds, nowUtc));
    }

    /// <summary>INV: only a published article can be archived; archiving twice is a no-op (no duplicate event, story AC-02). Archived
    /// articles remain searchable indefinitely (retention = archival, handover section 2 "Archive").</summary>
    public void Archive(Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.NewsAdminOnly, ErrorCodes.NewsForbidden));
        if (Status == NewsStatus.Archived)
        {
            return;
        }

        Check(new BusinessRule(RuleCodes.NewsMustBePublishedToArchive, "Only a published article can be archived.", Status != NewsStatus.Published,
            kind: BusinessRuleKind.Conflict));

        Status = NewsStatus.Archived;
        ArchivedAtUtc = nowUtc;
        Raise(new NewsArticleArchivedDomainEvent(Id, actor.Id, nowUtc));
    }

    private static IBusinessRule RequiredFieldsRule(LocalizedText title, LocalizedText body) =>
        new BusinessRule(RuleCodes.NewsRequiredField, "Title and body are required.", title.IsEmpty || body.IsEmpty, ErrorCodes.NewsRequiredField);
}
