using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application.UnitTests;

public class NewsHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-01")]
    public async Task CreateNewsArticleHandler_New_CreatesDraft()
    {
        var store = new FakeStore();
        var handler = new CreateNewsArticleHandler(store, Kit.User(), Kit.Clock());

        var result = await handler.Handle(new CreateNewsArticleCommand(NewsKind.Article, null, "Title", null, "Body"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Existing.Should().BeFalse();
        store.Articles.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-03")]
    public async Task CreateNewsArticleHandler_IdenticalDraftTwice_UpdatesInsteadOfDuplicating()
    {
        var store = new FakeStore();
        var handler = new CreateNewsArticleHandler(store, Kit.User(), Kit.Clock());
        var command = new CreateNewsArticleCommand(NewsKind.Article, null, "Title", null, "Body");

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        store.Articles.Should().ContainSingle();
        second.Value.Existing.Should().BeTrue();
        second.Value.Article.NewsArticleId.Should().Be(first.Value.Article.NewsArticleId);
    }

    [Fact]
    public async Task EditNewsArticleHandler_UnknownId_ReturnsNotFound()
    {
        var handler = new EditNewsArticleHandler(new FakeStore(), Kit.User());

        var result = await handler.Handle(new EditNewsArticleCommand(Guid.NewGuid(), null, "T", null, "B"), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-03")]
    public async Task AddNewsMediaHandler_StoresFileAndReturnsView()
    {
        var store = new FakeStore();
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new("A", "T"), new("A", "B"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.Articles.Add(article);
        var handler = new AddNewsMediaHandler(store, new FakeMediaStorage(), Kit.User());

        var result = await handler.Handle(new AddNewsMediaCommand(article.Id, NewsMediaType.Image, "a.png", "image/png", 100, new byte[100], "alt"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        article.Media.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-04")]
    public async Task PublishNewsArticleHandler_PassesCategoryIdsFromCategorization()
    {
        var store = new FakeStore();
        var admin = new Domain.Common.Actor(Guid.NewGuid(), true);
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), admin, Kit.Clock().GetUtcNow().UtcDateTime);
        store.Articles.Add(article);
        var categoryId = Guid.NewGuid();
        var categorization = ContentCategorization.CreateFor(article.Id);
        categorization.Assign(new[] { categoryId }, Array.Empty<string>(), admin);
        store.Categorizations.Add(categorization);
        var handler = new PublishNewsArticleHandler(store, store, Kit.User(), Kit.Clock());

        var result = await handler.Handle(new PublishNewsArticleCommand(article.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        article.DomainEvents.OfType<NewsArticlePublishedDomainEvent>().Single().CategoryIds.Should().Contain(categoryId);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-06")]
    public async Task ArchiveNewsArticleHandler_UnknownId_ReturnsNotFound()
    {
        var handler = new ArchiveNewsArticleHandler(new FakeStore(), Kit.User(), Kit.Clock());

        var result = await handler.Handle(new ArchiveNewsArticleCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    public async Task UpdateContentCategorizationHandler_CreatesCategorizationWhenMissing()
    {
        var store = new FakeStore();
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.Articles.Add(article);
        var handler = new UpdateContentCategorizationHandler(store, store, Kit.User());
        var categoryId = Guid.NewGuid();

        var result = await handler.Handle(new UpdateContentCategorizationCommand(article.Id, new[] { categoryId }, new[] { "tag1" }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Categorizations.Should().ContainSingle().Which.CategoryIds.Should().Contain(categoryId);
    }

    [Fact]
    public async Task CreateAndDeleteContentCategory_RoundTrips()
    {
        var store = new FakeStore();
        var createHandler = new CreateContentCategoryHandler(store, Kit.User());
        var created = await createHandler.Handle(new CreateContentCategoryCommand(null, "Announcements"), CancellationToken.None);

        var deleteHandler = new DeleteContentCategoryHandler(store, Kit.User());
        var deleted = await deleteHandler.Handle(new DeleteContentCategoryCommand(created.Value.CategoryId), CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue();
        store.Categories.Single().IsDeleted.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    public async Task GetNewsArticleHandler_DraftArticle_ReturnsNotFound()
    {
        var store = new FakeReadStore
        {
            NewsArticle = id => new NewsArticleView(id, "Article", new(null, "T"), new(null, "B"), "Draft", Array.Empty<NewsMediaView>(),
                Array.Empty<Guid>(), Array.Empty<string>(), Guid.NewGuid(), DateTime.UtcNow, null, null, Array.Empty<byte>())
        };
        var handler = new GetNewsArticleHandler(store);

        var result = await handler.Handle(new GetNewsArticleQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-05")]
    [Trait("AC", "AC-01")]
    public async Task GetNewsFeedHandler_WithInterestTags_ReturnsPersonalizedFeed()
    {
        var personalized = new[] { new NewsListItemView(Guid.NewGuid(), "Article", new(null, "T"), "Published", Array.Empty<string>(), DateTime.UtcNow) };
        var store = new FakeReadStore { PersonalizedFeed = _ => personalized };
        var interests = new FakeProfileInterestsProvider { Tags = new[] { "software" } };
        var handler = new GetNewsFeedHandler(store, interests, Kit.User(ActorType.JobSeeker));

        var result = await handler.Handle(new GetNewsFeedQuery(), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(personalized);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-05")]
    [Trait("AC", "AC-02")]
    public async Task GetNewsFeedHandler_WithoutInterestTags_FallsBackToGeneralFeed()
    {
        var general = new[] { new NewsListItemView(Guid.NewGuid(), "Article", new(null, "T"), "Published", Array.Empty<string>(), DateTime.UtcNow) };
        var store = new FakeReadStore { GeneralFeed = () => general };
        var handler = new GetNewsFeedHandler(store, new FakeProfileInterestsProvider(), Kit.User(ActorType.JobSeeker));

        var result = await handler.Handle(new GetNewsFeedQuery(), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(general);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-05")]
    public async Task GetNewsFeedHandler_PersonalizedFeedEmpty_FallsBackToGeneralFeed()
    {
        var general = new[] { new NewsListItemView(Guid.NewGuid(), "Article", new(null, "T"), "Published", Array.Empty<string>(), DateTime.UtcNow) };
        var store = new FakeReadStore { GeneralFeed = () => general, PersonalizedFeed = _ => Array.Empty<NewsListItemView>() };
        var interests = new FakeProfileInterestsProvider { Tags = new[] { "unmatched-tag" } };
        var handler = new GetNewsFeedHandler(store, interests, Kit.User(ActorType.JobSeeker));

        var result = await handler.Handle(new GetNewsFeedQuery(), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(general);
    }
}
