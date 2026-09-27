using System.Reflection;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.HelpContent.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddHelpContentApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.HelpContentEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.IDomainEventMapper>(sp => sp.GetRequiredService<Events.HelpContentEventMapper>());
        return services;
    }
}

/// <summary>Administrator command/query: authoring surface of both News and Help/FAQ (handover section 6.1, ADMIN_ONLY family).</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    /// <summary>Overridden per-request where the handover distinguishes E-NEWSU-FORBIDDEN from E-FAQHC-FORBIDDEN.</summary>
    public virtual string ForbiddenErrorCode => ErrorCodes.NewsForbidden;
}

public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public virtual string ForbiddenErrorCode => ErrorCodes.NewsForbidden;
}

/// <summary>Any authenticated caller (job seeker, employer, external site or administrator) - e.g. feedback, tutorials, company-page edit.</summary>
public abstract record AuthenticatedCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest;

public abstract record AuthenticatedQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest;

public static class ActorFactory
{
    public static Actor From(SharedKernel.Application.Ports.ICurrentUser user) => new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

// ---------------------------------------------------------------------- read models

public sealed record LocalizedView(string? Ar, string? En);

public sealed record NewsMediaView(Guid MediaId, string Type, string Url, string? AltText);

public sealed record NewsArticleView(
    Guid NewsArticleId, string Kind, LocalizedView Title, LocalizedView Body, string Status, IReadOnlyList<NewsMediaView> Media,
    IReadOnlyList<Guid> CategoryIds, IReadOnlyList<string> Tags, Guid CreatedBy, DateTime CreatedAtUtc, DateTime? PublishedAtUtc,
    DateTime? ArchivedAtUtc, byte[] RowVersion);

public sealed record NewsListItemView(
    Guid NewsArticleId, string Kind, LocalizedView Title, string Status, IReadOnlyList<string> CategoryNames, DateTime? PublishedAtUtc);

public sealed record ContentCategoryView(Guid CategoryId, LocalizedView Name, bool IsDeleted);

public sealed record HelpMediaView(Guid MediaId, string Type, string Url, string? CaptionsRef, string? TextAlternative);

public sealed record HelpContentVersionView(int VersionNo, LocalizedView Title, LocalizedView Body, Guid EditedBy, DateTime EditedAtUtc);

public sealed record HelpContentView(
    Guid HelpContentId, string Kind, LocalizedView Title, LocalizedView Body, int CurrentVersion, Guid? TopicId, IReadOnlyList<string> Roles,
    IReadOnlyList<HelpMediaView> Media, byte[] RowVersion);

public sealed record HelpSearchResultView(Guid HelpContentId, string Kind, LocalizedView Title, string? TopicName);

public sealed record HelpTopicView(Guid TopicId, LocalizedView Name, bool IsRemoved);

public sealed record HelpCenterTopicView(Guid? TopicId, string? TopicName, IReadOnlyList<HelpSearchResultView> Items);

public sealed record HelpFeedbackSummaryView(Guid HelpContentId, int HelpfulCount, int NotHelpfulCount);

public sealed record TutorialView(Guid TutorialId, LocalizedView Title, LocalizedView Body, bool Completed, DateTime? CompletedAtUtc);

public sealed record OpenPostingView(Guid JobPostingId, string Title, string? Governorate, string? City, DateTime DeadlineUtc);

public sealed record CompanyPageView(
    Guid EmployerAccountId, string Name, string? LogoUrl, string Industry, string CompanySize, string Website, bool Verified, string? Badge,
    LocalizedView Background, IReadOnlyList<string> Highlights, IReadOnlyList<OpenPostingView> OpenPostings, bool OpenPostingsDegraded);

// ---------------------------------------------------------------------- ports

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

/// <summary>Stores an uploaded media file's bytes and returns its storage key; content is addressed by key, never re-derived.</summary>
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default);

    string UrlFor(string storageKey);
}

/// <summary>Cache-aside port (foundation section 10, handover section 9); implemented in Infrastructure over ICacheStore.</summary>
public interface IHelpContentCache
{
    Task<CompanyPageView?> GetCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default);

    Task SetCompanyPageAsync(Guid employerAccountId, CompanyPageView view, CancellationToken ct = default);

    Task InvalidateCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default);
}

/// <summary>Q-05 (resolved: BC-09 now exposes GET /internal/v1/employers/{id}/open-postings): current job openings for the company page,
/// degrading to an empty list when BC-09 is unavailable (handover section 6.2).</summary>
public interface IOpenPostingsProvider
{
    Task<(IReadOnlyList<OpenPostingView> Items, bool Degraded)> GetOpenPostingsAsync(Guid employerAccountId, CancellationToken ct = default);
}

/// <summary>Company info and verification standing from BC-05's internal API (handover section 6.2).</summary>
public interface ICompanyDirectoryProvider
{
    Task<CompanyDirectoryEntry?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default);
}

public sealed record CompanyDirectoryEntry(string Name, string? LogoUrl, string Industry, string CompanySize, string Website, bool Verified, string? Badge);

/// <summary>Q-06 (proposed): BC-04's profile interest tags for news-feed personalisation (handover section 6.2), falling back to the
/// general feed when unavailable.</summary>
public interface IProfileInterestsProvider
{
    Task<IReadOnlyList<string>> GetInterestTagsAsync(Guid jobSeekerAccountId, CancellationToken ct = default);
}
