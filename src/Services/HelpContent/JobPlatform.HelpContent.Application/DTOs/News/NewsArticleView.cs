using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.News;

public sealed record NewsArticleView(
    Guid NewsArticleId, string Kind, LocalizedView Title, LocalizedView Body, string Status, IReadOnlyList<NewsMediaView> Media,
    IReadOnlyList<Guid> CategoryIds, IReadOnlyList<string> Tags, Guid CreatedBy, DateTime CreatedAtUtc, DateTime? PublishedAtUtc,
    DateTime? ArchivedAtUtc, byte[] RowVersion);
