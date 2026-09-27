using FluentValidation;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application;

// ---------------------------------------------------------------------- commands: authoring

public sealed record NewsArticleMutationResult(NewsArticleView Article, bool Existing);

public sealed record CreateNewsArticleCommand(NewsKind Kind, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn)
    : AdminCommand<NewsArticleMutationResult>;

public sealed class CreateNewsArticleValidator : AbstractValidator<CreateNewsArticleCommand>
{
    public CreateNewsArticleValidator()
    {
        Include(new TitleBodyRules<CreateNewsArticleCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("VAL.Kind.Invalid");
    }
}

public sealed record EditNewsArticleCommand(Guid NewsArticleId, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn)
    : AdminCommand<NewsArticleView>;

public sealed class EditNewsArticleValidator : AbstractValidator<EditNewsArticleCommand>
{
    public EditNewsArticleValidator() => Include(new TitleBodyRules<EditNewsArticleCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
}

/// <summary>Shared title/body validation for both News and Help authoring commands (handover section 7).</summary>
internal sealed class TitleBodyRules<T> : AbstractValidator<T>
{
    public TitleBodyRules(System.Linq.Expressions.Expression<Func<T, string?>> titleAr, System.Linq.Expressions.Expression<Func<T, string?>> titleEn,
        System.Linq.Expressions.Expression<Func<T, string?>> bodyAr, System.Linq.Expressions.Expression<Func<T, string?>> bodyEn)
    {
        var getTitleAr = titleAr.Compile();
        var getTitleEn = titleEn.Compile();
        var getBodyAr = bodyAr.Compile();
        var getBodyEn = bodyEn.Compile();

        RuleFor(titleAr).MaximumLength(200).WithErrorCode("VAL.TitleAr.TooLong");
        RuleFor(titleEn).MaximumLength(200).WithErrorCode("VAL.TitleEn.TooLong");
        RuleFor(bodyAr).MaximumLength(50_000).WithErrorCode("VAL.BodyAr.TooLong");
        RuleFor(bodyEn).MaximumLength(50_000).WithErrorCode("VAL.BodyEn.TooLong");
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(getTitleAr(c)) || !string.IsNullOrWhiteSpace(getTitleEn(c)))
            .WithErrorCode("VAL.Title.Required").OverridePropertyName("Title");
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(getBodyAr(c)) || !string.IsNullOrWhiteSpace(getBodyEn(c)))
            .WithErrorCode("VAL.Body.Required").OverridePropertyName("Body");
    }
}

internal sealed class CreateNewsArticleHandler : ICommandHandler<CreateNewsArticleCommand, NewsArticleMutationResult>
{
    private readonly INewsArticleRepository _articles;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreateNewsArticleHandler(INewsArticleRepository articles, ICurrentUser user, TimeProvider clock)
    {
        _articles = articles;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<NewsArticleMutationResult>> Handle(CreateNewsArticleCommand request, CancellationToken ct)
    {
        var title = new LocalizedText(request.TitleAr, request.TitleEn);
        var body = new LocalizedText(request.BodyAr, request.BodyEn);
        var hash = NewsArticle.ComputeContentHash(title, body);

        // INV-02: an identical draft resubmitted updates the existing draft instead of creating a duplicate.
        var existing = await _articles.GetDraftByContentHashAsync(hash, ct);
        if (existing is not null)
        {
            existing.EditDraft(title, body, ActorFactory.From(_user));
            return new NewsArticleMutationResult(ToView(existing), true);
        }

        var article = NewsArticle.CreateDraft(Guid.NewGuid(), request.Kind, title, body, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _articles.Add(article);
        return new NewsArticleMutationResult(ToView(article), false);
    }

    internal static NewsArticleView ToView(NewsArticle a) => new(
        a.Id, a.Kind.ToString(), new LocalizedView(a.Title.Ar, a.Title.En), new LocalizedView(a.Body.Ar, a.Body.En), a.Status.ToString(),
        a.Media.Select(m => new NewsMediaView(m.Id, m.Type.ToString(), m.File.StorageKey, m.AltText)).ToArray(),
        Array.Empty<Guid>(), Array.Empty<string>(), a.CreatedBy, a.CreatedAtUtc, a.PublishedAtUtc, a.ArchivedAtUtc, a.RowVersion);
}

internal sealed class EditNewsArticleHandler : ICommandHandler<EditNewsArticleCommand, NewsArticleView>
{
    private readonly INewsArticleRepository _articles;
    private readonly ICurrentUser _user;

    public EditNewsArticleHandler(INewsArticleRepository articles, ICurrentUser user)
    {
        _articles = articles;
        _user = user;
    }

    public async Task<Result<NewsArticleView>> Handle(EditNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        article.EditDraft(new LocalizedText(request.TitleAr, request.TitleEn), new LocalizedText(request.BodyAr, request.BodyEn), ActorFactory.From(_user));
        return CreateNewsArticleHandler.ToView(article);
    }
}

public sealed record AddNewsMediaCommand(Guid NewsArticleId, NewsMediaType Type, string FileName, string ContentType, long SizeBytes, byte[] Content,
    string? AltText) : AdminCommand<NewsMediaView>;

public sealed class AddNewsMediaValidator : AbstractValidator<AddNewsMediaCommand>
{
    public AddNewsMediaValidator()
    {
        RuleFor(c => c.Type).IsInEnum().WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260).WithErrorCode("VAL.FileName.Required");
        RuleFor(c => c.SizeBytes).GreaterThan(0).LessThanOrEqualTo(NewsArticle.MaxMediaSizeBytes).WithErrorCode("VAL.SizeBytes.TooLarge");
        RuleFor(c => c.AltText).NotEmpty().When(c => c.Type == NewsMediaType.Image).WithErrorCode("VAL.AltText.RequiredForImages");
    }
}

internal sealed class AddNewsMediaHandler : ICommandHandler<AddNewsMediaCommand, NewsMediaView>
{
    private readonly INewsArticleRepository _articles;
    private readonly IMediaStorage _storage;
    private readonly ICurrentUser _user;

    public AddNewsMediaHandler(INewsArticleRepository articles, IMediaStorage storage, ICurrentUser user)
    {
        _articles = articles;
        _storage = storage;
        _user = user;
    }

    public async Task<Result<NewsMediaView>> Handle(AddNewsMediaCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        await using var stream = new MemoryStream(request.Content);
        var storageKey = await _storage.SaveAsync(stream, request.FileName, request.ContentType, ct);
        var file = new NewsMediaFile(storageKey, request.SizeBytes, request.ContentType);
        var media = article.AddMedia(Guid.NewGuid(), request.Type, file, request.AltText, ActorFactory.From(_user));
        return new NewsMediaView(media.Id, media.Type.ToString(), _storage.UrlFor(storageKey), media.AltText);
    }
}

public sealed record PublishNewsArticleCommand(Guid NewsArticleId) : AdminCommand<Unit>;

internal sealed class PublishNewsArticleHandler : ICommandHandler<PublishNewsArticleCommand, Unit>
{
    private readonly INewsArticleRepository _articles;
    private readonly IContentCategorizationRepository _categorizations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PublishNewsArticleHandler(INewsArticleRepository articles, IContentCategorizationRepository categorizations, ICurrentUser user, TimeProvider clock)
    {
        _articles = articles;
        _categorizations = categorizations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(PublishNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        var categorization = await _categorizations.GetByArticleAsync(article.Id, ct);
        article.Publish(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime, categorization?.CategoryIds ?? Array.Empty<Guid>());
        return Result.Success();
    }
}

public sealed record ArchiveNewsArticleCommand(Guid NewsArticleId) : AdminCommand<Unit>;

internal sealed class ArchiveNewsArticleHandler : ICommandHandler<ArchiveNewsArticleCommand, Unit>
{
    private readonly INewsArticleRepository _articles;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ArchiveNewsArticleHandler(INewsArticleRepository articles, ICurrentUser user, TimeProvider clock)
    {
        _articles = articles;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ArchiveNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        article.Archive(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- commands: categorization catalogue

public sealed record UpdateContentCategorizationCommand(Guid NewsArticleId, IReadOnlyList<Guid> CategoryIds, IReadOnlyList<string> Tags) : AdminCommand<Unit>;

public sealed class UpdateContentCategorizationValidator : AbstractValidator<UpdateContentCategorizationCommand>
{
    public UpdateContentCategorizationValidator()
    {
        RuleFor(c => c.Tags).Must(t => t.Count <= 20).WithErrorCode("VAL.Tags.TooMany");
        RuleForEach(c => c.Tags).MaximumLength(50).WithErrorCode("VAL.Tag.TooLong");
    }
}

internal sealed class UpdateContentCategorizationHandler : ICommandHandler<UpdateContentCategorizationCommand, Unit>
{
    private readonly INewsArticleRepository _articles;
    private readonly IContentCategorizationRepository _categorizations;
    private readonly ICurrentUser _user;

    public UpdateContentCategorizationHandler(INewsArticleRepository articles, IContentCategorizationRepository categorizations, ICurrentUser user)
    {
        _articles = articles;
        _categorizations = categorizations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(UpdateContentCategorizationCommand request, CancellationToken ct)
    {
        if (await _articles.GetByIdAsync(request.NewsArticleId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        var categorization = await _categorizations.GetByArticleAsync(request.NewsArticleId, ct);
        if (categorization is null)
        {
            categorization = ContentCategorization.CreateFor(request.NewsArticleId);
            _categorizations.Add(categorization);
        }

        categorization.Assign(request.CategoryIds, request.Tags, ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record CreateContentCategoryCommand(string? NameAr, string? NameEn) : AdminCommand<ContentCategoryView>;

internal sealed class CreateContentCategoryHandler : ICommandHandler<CreateContentCategoryCommand, ContentCategoryView>
{
    private readonly IContentCategoryRepository _categories;
    private readonly ICurrentUser _user;

    public CreateContentCategoryHandler(IContentCategoryRepository categories, ICurrentUser user)
    {
        _categories = categories;
        _user = user;
    }

    public async Task<Result<ContentCategoryView>> Handle(CreateContentCategoryCommand request, CancellationToken ct)
    {
        var category = ContentCategory.Create(Guid.NewGuid(), new LocalizedText(request.NameAr, request.NameEn), ActorFactory.From(_user));
        _categories.Add(category);
        return await Task.FromResult(new ContentCategoryView(category.Id, new LocalizedView(category.Name.Ar, category.Name.En), category.IsDeleted));
    }
}

public sealed record DeleteContentCategoryCommand(Guid CategoryId) : AdminCommand<Unit>;

internal sealed class DeleteContentCategoryHandler : ICommandHandler<DeleteContentCategoryCommand, Unit>
{
    private readonly IContentCategoryRepository _categories;
    private readonly ICurrentUser _user;

    public DeleteContentCategoryHandler(IContentCategoryRepository categories, ICurrentUser user)
    {
        _categories = categories;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(DeleteContentCategoryCommand request, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The category was not found.");
        }

        category.SoftDelete(ActorFactory.From(_user));
        return Result.Success();
    }
}

public sealed record ListContentCategoriesQuery : AdminQuery<IReadOnlyList<ContentCategoryView>>;

internal sealed class ListContentCategoriesHandler : IQueryHandler<ListContentCategoriesQuery, IReadOnlyList<ContentCategoryView>>
{
    private readonly IHelpContentReadStore _store;

    public ListContentCategoriesHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<ContentCategoryView>>> Handle(ListContentCategoriesQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListCategoriesAsync(ct));
}

// ---------------------------------------------------------------------- queries: public reads

public sealed record ListNewsQuery(Guid? CategoryId, int Page = 1, int PageSize = 20) : IQuery<PagedResult<NewsListItemView>>;

internal sealed class ListNewsHandler : IQueryHandler<ListNewsQuery, PagedResult<NewsListItemView>>
{
    private readonly IHelpContentReadStore _store;

    public ListNewsHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<PagedResult<NewsListItemView>>> Handle(ListNewsQuery request, CancellationToken ct) =>
        await _store.ListNewsAsync(request.CategoryId, NewsStatus.Published.ToString(), new PageRequest(request.Page, request.PageSize), ct);
}

public sealed record GetNewsArticleQuery(Guid NewsArticleId) : IQuery<NewsArticleView>;

internal sealed class GetNewsArticleHandler : IQueryHandler<GetNewsArticleQuery, NewsArticleView>
{
    private readonly IHelpContentReadStore _store;

    public GetNewsArticleHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<NewsArticleView>> Handle(GetNewsArticleQuery request, CancellationToken ct)
    {
        var view = await _store.GetNewsArticleAsync(request.NewsArticleId, ct);
        // A draft is not yet public content (handover section 2: "Draft" is not one of the reader-visible states).
        return view is null || view.Status == NewsStatus.Draft.ToString()
            ? Error.NotFound(ErrorCodes.NotFound, "The news article was not found.")
            : view;
    }
}

public sealed record SearchNewsArchiveQuery(string? Keyword, int Page = 1, int PageSize = 20) : IQuery<PagedResult<NewsListItemView>>;

internal sealed class SearchNewsArchiveHandler : IQueryHandler<SearchNewsArchiveQuery, PagedResult<NewsListItemView>>
{
    private readonly IHelpContentReadStore _store;

    public SearchNewsArchiveHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<PagedResult<NewsListItemView>>> Handle(SearchNewsArchiveQuery request, CancellationToken ct) =>
        await _store.SearchNewsArchiveAsync(request.Keyword, new PageRequest(request.Page, request.PageSize), ct);
}

/// <summary>US-3.7.1-05: personalised feed when profile interests are available (Q-06), else the general feed - never an error
/// (handover section 6.2: "fallback to general feed").</summary>
public sealed record GetNewsFeedQuery : AuthenticatedQuery<IReadOnlyList<NewsListItemView>>
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
}

internal sealed class GetNewsFeedHandler : IQueryHandler<GetNewsFeedQuery, IReadOnlyList<NewsListItemView>>
{
    private const int FeedSize = 20;

    private readonly IHelpContentReadStore _store;
    private readonly IProfileInterestsProvider _interests;
    private readonly ICurrentUser _user;

    public GetNewsFeedHandler(IHelpContentReadStore store, IProfileInterestsProvider interests, ICurrentUser user)
    {
        _store = store;
        _interests = interests;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<NewsListItemView>>> Handle(GetNewsFeedQuery request, CancellationToken ct)
    {
        var tags = _user.UserId is { } id ? await _interests.GetInterestTagsAsync(id, ct) : Array.Empty<string>();
        if (tags.Count == 0)
        {
            return Result.Success(await _store.GetGeneralFeedAsync(FeedSize, ct));
        }

        var personalized = await _store.GetPersonalizedFeedAsync(tags, FeedSize, ct);
        return Result.Success(personalized.Count > 0 ? personalized : await _store.GetGeneralFeedAsync(FeedSize, ct));
    }
}
