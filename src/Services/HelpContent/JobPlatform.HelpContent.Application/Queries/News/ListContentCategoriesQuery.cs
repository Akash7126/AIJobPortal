using JobPlatform.HelpContent.Application.DTOs.News;

namespace JobPlatform.HelpContent.Application.Queries.News;

public sealed record ListContentCategoriesQuery : AdminQuery<IReadOnlyList<ContentCategoryView>>;
