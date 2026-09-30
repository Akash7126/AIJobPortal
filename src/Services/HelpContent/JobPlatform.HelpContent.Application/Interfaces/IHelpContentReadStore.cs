using JobPlatform.HelpContent.Application.DTOs.Feedback;
using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IHelpContentReadStore
{
    Task<NewsArticleView?> GetNewsArticleAsync(Guid id, CancellationToken ct = default);

    Task<SharedKernel.Application.Paging.PagedResult<NewsListItemView>> ListNewsAsync(Guid? categoryId, string? status,
        SharedKernel.Application.Paging.PageRequest page, CancellationToken ct = default);

    Task<SharedKernel.Application.Paging.PagedResult<NewsListItemView>> SearchNewsArchiveAsync(string? keyword,
        SharedKernel.Application.Paging.PageRequest page, CancellationToken ct = default);

    Task<IReadOnlyList<NewsListItemView>> GetGeneralFeedAsync(int take, CancellationToken ct = default);

    Task<IReadOnlyList<NewsListItemView>> GetPersonalizedFeedAsync(IReadOnlyList<string> interestTags, int take, CancellationToken ct = default);

    Task<IReadOnlyList<ContentCategoryView>> ListCategoriesAsync(CancellationToken ct = default);

    Task<HelpContentView?> GetHelpContentAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<HelpCenterTopicView>> GetHelpCenterAsync(HelpRole? role, CancellationToken ct = default);

    Task<SharedKernel.Application.Paging.PagedResult<HelpSearchResultView>> SearchHelpContentAsync(string keyword, HelpRole? role,
        SharedKernel.Application.Paging.PageRequest page, CancellationToken ct = default);

    Task<IReadOnlyList<HelpTopicView>> ListHelpTopicsAsync(CancellationToken ct = default);

    Task<HelpFeedbackSummaryView?> GetFeedbackSummaryAsync(Guid helpContentId, CancellationToken ct = default);
}
