using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.HelpContent.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real aggregate state (no mocking of domain behaviour).</summary>
public sealed class FakeStore : INewsArticleRepository, IContentCategoryRepository, IContentCategorizationRepository, IHelpContentRepository,
    IHelpTopicRepository, IHelpOrganizationRepository, IHelpFeedbackRepository, ITutorialProgressRepository, IContextHelpMappingRepository,
    ICompanyProfilePageRepository
{
    public List<NewsArticle> Articles { get; } = new();
    public List<ContentCategory> Categories { get; } = new();
    public List<ContentCategorization> Categorizations { get; } = new();
    public List<Domain.HelpContent> HelpContents { get; } = new();
    public List<HelpTopic> Topics { get; } = new();
    public List<HelpContentOrganization> Organizations { get; } = new();
    public List<HelpFeedback> Feedback { get; } = new();
    public List<TutorialProgress> TutorialProgresses { get; } = new();
    public List<ContextHelpMapping> ContextHelpMappings { get; } = new();
    public List<CompanyProfilePage> CompanyPages { get; } = new();

    Task<NewsArticle?> INewsArticleRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Articles.FirstOrDefault(a => a.Id == id));

    public Task<NewsArticle?> GetDraftByContentHashAsync(string contentHash, CancellationToken ct = default) =>
        Task.FromResult(Articles.FirstOrDefault(a => a.ContentHash == contentHash && a.Status == NewsStatus.Draft));

    void INewsArticleRepository.Add(NewsArticle article) => Articles.Add(article);

    Task<ContentCategory?> IContentCategoryRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<ContentCategory>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ContentCategory>>(Categories);

    void IContentCategoryRepository.Add(ContentCategory category) => Categories.Add(category);

    public Task<ContentCategorization?> GetByArticleAsync(Guid articleId, CancellationToken ct = default) =>
        Task.FromResult(Categorizations.FirstOrDefault(c => c.ArticleId == articleId));

    void IContentCategorizationRepository.Add(ContentCategorization categorization) => Categorizations.Add(categorization);

    Task<Domain.HelpContent?> IHelpContentRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(HelpContents.FirstOrDefault(c => c.Id == id));

    void IHelpContentRepository.Add(Domain.HelpContent content) => HelpContents.Add(content);

    Task<HelpTopic?> IHelpTopicRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Topics.FirstOrDefault(t => t.Id == id));

    Task<IReadOnlyList<HelpTopic>> IHelpTopicRepository.ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HelpTopic>>(Topics);

    void IHelpTopicRepository.Add(HelpTopic topic) => Topics.Add(topic);

    public Task<HelpContentOrganization?> GetAsync(Guid helpContentId, CancellationToken ct = default) =>
        Task.FromResult(Organizations.FirstOrDefault(o => o.HelpContentId == helpContentId));

    void IHelpOrganizationRepository.Add(HelpContentOrganization organization) => Organizations.Add(organization);

    public Task<HelpFeedback?> GetByUserAndContentAsync(Guid userId, Guid helpContentId, CancellationToken ct = default) =>
        Task.FromResult(Feedback.FirstOrDefault(f => f.UserId == userId && f.HelpContentId == helpContentId));

    void IHelpFeedbackRepository.Add(HelpFeedback feedback) => Feedback.Add(feedback);

    public Task<TutorialProgress?> GetAsync(Guid userId, Guid tutorialId, CancellationToken ct = default) =>
        Task.FromResult(TutorialProgresses.FirstOrDefault(p => p.UserId == userId && p.TutorialId == tutorialId));

    void ITutorialProgressRepository.Add(TutorialProgress progress) => TutorialProgresses.Add(progress);

    public Task<ContextHelpMapping?> GetByPageKeyAsync(string pageKey, CancellationToken ct = default) =>
        Task.FromResult(ContextHelpMappings.FirstOrDefault(m => m.PageKey == pageKey));

    void IContextHelpMappingRepository.Add(ContextHelpMapping mapping) => ContextHelpMappings.Add(mapping);

    public Task<CompanyProfilePage?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(CompanyPages.FirstOrDefault(p => p.EmployerAccountId == employerAccountId));

    void ICompanyProfilePageRepository.Add(CompanyProfilePage page) => CompanyPages.Add(page);
}

public sealed class FakeReadStore : IHelpContentReadStore
{
    public Func<Guid, NewsArticleView?> NewsArticle { get; set; } = _ => null;
    public Func<IReadOnlyList<NewsListItemView>> GeneralFeed { get; set; } = () => Array.Empty<NewsListItemView>();
    public Func<IReadOnlyList<string>, IReadOnlyList<NewsListItemView>> PersonalizedFeed { get; set; } = _ => Array.Empty<NewsListItemView>();
    public Func<Guid, HelpContentView?> HelpContent { get; set; } = _ => null;
    public Func<Guid, HelpFeedbackSummaryView?> FeedbackSummary { get; set; } = _ => null;

    public Task<NewsArticleView?> GetNewsArticleAsync(Guid id, CancellationToken ct = default) => Task.FromResult(NewsArticle(id));

    public Task<PagedResult<NewsListItemView>> ListNewsAsync(Guid? categoryId, string? status, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<NewsListItemView>(Array.Empty<NewsListItemView>(), page.Page, page.PageSize, 0));

    public Task<PagedResult<NewsListItemView>> SearchNewsArchiveAsync(string? keyword, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<NewsListItemView>(Array.Empty<NewsListItemView>(), page.Page, page.PageSize, 0));

    public Task<IReadOnlyList<NewsListItemView>> GetGeneralFeedAsync(int take, CancellationToken ct = default) => Task.FromResult(GeneralFeed());

    public Task<IReadOnlyList<NewsListItemView>> GetPersonalizedFeedAsync(IReadOnlyList<string> interestTags, int take, CancellationToken ct = default) =>
        Task.FromResult(PersonalizedFeed(interestTags));

    public Task<IReadOnlyList<ContentCategoryView>> ListCategoriesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ContentCategoryView>>(Array.Empty<ContentCategoryView>());

    public Task<HelpContentView?> GetHelpContentAsync(Guid id, CancellationToken ct = default) => Task.FromResult(HelpContent(id));

    public Task<IReadOnlyList<HelpCenterTopicView>> GetHelpCenterAsync(HelpRole? role, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HelpCenterTopicView>>(Array.Empty<HelpCenterTopicView>());

    public Task<PagedResult<HelpSearchResultView>> SearchHelpContentAsync(string keyword, HelpRole? role, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<HelpSearchResultView>(Array.Empty<HelpSearchResultView>(), page.Page, page.PageSize, 0));

    public Task<IReadOnlyList<HelpTopicView>> ListHelpTopicsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HelpTopicView>>(Array.Empty<HelpTopicView>());

    public Task<HelpFeedbackSummaryView?> GetFeedbackSummaryAsync(Guid helpContentId, CancellationToken ct = default) => Task.FromResult(FeedbackSummary(helpContentId));
}

public sealed class FakeMediaStorage : IMediaStorage
{
    public Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default) =>
        Task.FromResult("stored/" + suggestedFileName);

    public string UrlFor(string storageKey) => "https://files.local/" + storageKey;
}

public sealed class FakeHelpContentCache : IHelpContentCache
{
    public Dictionary<Guid, CompanyPageView> Pages { get; } = new();

    public Task<CompanyPageView?> GetCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Pages.GetValueOrDefault(employerAccountId));

    public Task SetCompanyPageAsync(Guid employerAccountId, CompanyPageView view, CancellationToken ct = default)
    {
        Pages[employerAccountId] = view;
        return Task.CompletedTask;
    }

    public Task InvalidateCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        Pages.Remove(employerAccountId);
        return Task.CompletedTask;
    }
}

public sealed class FakeCompanyDirectoryProvider : ICompanyDirectoryProvider
{
    public CompanyDirectoryEntry? Entry { get; set; }

    public Task<CompanyDirectoryEntry?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default) => Task.FromResult(Entry);
}

public sealed class FakeOpenPostingsProvider : IOpenPostingsProvider
{
    public IReadOnlyList<OpenPostingView> Items { get; set; } = Array.Empty<OpenPostingView>();
    public bool Degraded { get; set; }

    public Task<(IReadOnlyList<OpenPostingView> Items, bool Degraded)> GetOpenPostingsAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult((Items, Degraded));
}

public sealed class FakeProfileInterestsProvider : IProfileInterestsProvider
{
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public Task<IReadOnlyList<string>> GetInterestTagsAsync(Guid jobSeekerAccountId, CancellationToken ct = default) => Task.FromResult(Tags);
}

public static class Kit
{
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType? actor = ActorType.Administrator, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? Guid.NewGuid());
        user.MfaVerified.Returns(actor == ActorType.Administrator);
        return user;
    }
}
