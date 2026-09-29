using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.News;

public sealed record NewsListItemView(
    Guid NewsArticleId, string Kind, LocalizedView Title, string Status, IReadOnlyList<string> CategoryNames, DateTime? PublishedAtUtc);
