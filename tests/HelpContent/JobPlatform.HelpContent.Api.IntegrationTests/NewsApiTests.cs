using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.HelpContent.Api.IntegrationTests;

public class NewsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NewsApiTests(ApiFactory factory) => _factory = factory;

    private static object NewsBody(string titleEn = "Title", string bodyEn = "Body", string kind = "Article") =>
        new { kind, titleAr = (string?)null, titleEn, bodyAr = (string?)null, bodyEn };

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-01")]
    public async Task FullLifecycle_CreatePublishArchive_PublishesAllThreeEvents()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());

        var create = await admin.PostJsonAsync("/api/v1/admin/news", NewsBody(titleEn: $"Breaking {Guid.NewGuid():N}"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var articleId = (await create.Json())["newsArticleId"]!.GetValue<Guid>();

        var publish = await admin.PostJsonAsync($"/api/v1/admin/news/{articleId}/publish");
        publish.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getPublished = await _factory.ClientFor(null).GetAsync($"/api/v1/news/{articleId}");
        getPublished.StatusCode.Should().Be(HttpStatusCode.OK);
        (await getPublished.Json())["status"]!.GetValue<string>().Should().Be("Published");

        var archive = await admin.PostJsonAsync($"/api/v1/admin/news/{articleId}/archive");
        archive.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "news-article.created.v1");
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "news-article.published.v1");
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "news-article.archived.v1");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-02")]
    public async Task Publish_Twice_IsIdempotent_204Both()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var articleId = (await (await admin.PostJsonAsync("/api/v1/admin/news", NewsBody(titleEn: $"Idempotent {Guid.NewGuid():N}"))).Json())["newsArticleId"]!.GetValue<Guid>();

        var first = await admin.PostJsonAsync($"/api/v1/admin/news/{articleId}/publish");
        var second = await admin.PostJsonAsync($"/api/v1/admin/news/{articleId}/publish");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-03")]
    public async Task Create_IdenticalDraftTwice_ReturnsExistingWithoutDuplicating()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var body = NewsBody(titleEn: $"Same {Guid.NewGuid():N}");

        var first = await admin.PostJsonAsync("/api/v1/admin/news", body);
        var second = await admin.PostJsonAsync("/api/v1/admin/news", body);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Json())["newsArticleId"]!.GetValue<Guid>().Should().Be((await second.Json())["newsArticleId"]!.GetValue<Guid>());
    }

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-02")]
    public async Task Create_MissingTitleAndBody_Is400()
    {
        var response = await _factory.ClientFor(TestTokens.Admin())
            .PostJsonAsync("/api/v1/admin/news", new { kind = "Article", titleAr = (string?)null, titleEn = (string?)null, bodyAr = (string?)null, bodyEn = (string?)null });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    public async Task Create_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).PostJsonAsync("/api/v1/admin/news", NewsBody());

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-NEWSU-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    public async Task Get_DraftArticle_Is404ForAnonymousReaders()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var articleId = (await (await admin.PostJsonAsync("/api/v1/admin/news", NewsBody(titleEn: $"Draft only {Guid.NewGuid():N}"))).Json())["newsArticleId"]!.GetValue<Guid>();

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/news/{articleId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-06")]
    [Trait("AC", "AC-05")]
    public async Task Archive_ThenSearchArchive_StillFindsIt()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var uniqueWord = $"Zephyr{Guid.NewGuid():N}";
        var articleId = (await (await admin.PostJsonAsync("/api/v1/admin/news", NewsBody(titleEn: uniqueWord))).Json())["newsArticleId"]!.GetValue<Guid>();
        await admin.PostJsonAsync($"/api/v1/admin/news/{articleId}/publish");
        await admin.PostJsonAsync($"/api/v1/admin/news/{articleId}/archive");

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/news/archive?q={uniqueWord}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Json())["items"]!.AsArray();
        items.Should().ContainSingle(i => i!["newsArticleId"]!.GetValue<Guid>() == articleId);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    public async Task Categorization_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer())
            .PutJsonAsync($"/api/v1/admin/news/{Guid.NewGuid()}/categorization", new { categoryIds = Array.Empty<Guid>(), tags = Array.Empty<string>() });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-NEWSU-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    public async Task ContentCategories_CreateListDelete_RoundTrips()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());

        var create = await admin.PostJsonAsync("/api/v1/admin/content-categories", new { nameAr = (string?)null, nameEn = $"Cat{Guid.NewGuid():N}" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryId = (await create.Json())["categoryId"]!.GetValue<Guid>();

        var list = await (await admin.GetAsync("/api/v1/admin/content-categories")).Json();
        list.AsArray().Should().Contain(c => c!["categoryId"]!.GetValue<Guid>() == categoryId);

        var delete = await admin.DeleteAsync($"/api/v1/admin/content-categories/{categoryId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-05")]
    public async Task Feed_ByJobSeeker_ReturnsGeneralFeedWhenNoInterests()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/news/feed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Feed_ByEmployer_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).GetAsync("/api/v1/news/feed");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
