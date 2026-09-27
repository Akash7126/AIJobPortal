using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class NewsArticleTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static LocalizedText Title(string? en = "Title", string? ar = null) => new(ar, en);

    private static LocalizedText Body(string? en = "Body", string? ar = null) => new(ar, en);

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-01")]
    public void CreateDraft_Valid_StartsAsDraftAndRaisesCreated()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);

        article.Status.Should().Be(NewsStatus.Draft);
        article.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<NewsArticleCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-02")]
    public void CreateDraft_MissingTitleAndBody_ThrowsRequiredField()
    {
        var act = () => NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(null, null), Body(null, null), Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.NewsRequiredField);
        ex.ExternalCode.Should().Be("E-NEWSU-REQUIRED-FIELD");
    }

    [Fact]
    public void CreateDraft_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), new Actor(Guid.NewGuid(), false), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.NewsAdminOnly);
        ex.ExternalCode.Should().Be("E-NEWSU-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-03")]
    public void ComputeContentHash_IsStableForIdenticalContent_AndDiffersForDifferentContent()
    {
        var h1 = NewsArticle.ComputeContentHash(Title("Same"), Body("Same body"));
        var h2 = NewsArticle.ComputeContentHash(Title("Same"), Body("Same body"));
        var h3 = NewsArticle.ComputeContentHash(Title("Different"), Body("Same body"));

        h1.Should().Be(h2);
        h1.Should().NotBe(h3);
    }

    [Fact]
    public void EditDraft_WhileDraft_UpdatesTitleAndBody()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title("Old"), Body("Old body"), Admin, At);

        article.EditDraft(Title("New"), Body("New body"), Admin);

        article.Title.En.Should().Be("New");
    }

    [Fact]
    public void EditDraft_AfterPublish_ThrowsEditOnlyDraft()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        article.Publish(Admin, At, Array.Empty<Guid>());

        var act = () => article.EditDraft(Title("New"), Body("New body"), Admin);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.NewsEditOnlyDraft);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-03")]
    [Trait("AC", "AC-02")]
    public void AddMedia_OversizedFile_ThrowsTooLarge()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        var file = new NewsMediaFile("key", NewsArticle.MaxMediaSizeBytes + 1, "image/png");

        var act = () => article.AddMedia(Guid.NewGuid(), NewsMediaType.Image, file, "alt", Admin);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.NewsMediaTooLarge);
        ex.ExternalCode.Should().Be("E-NEWSU-TOO-LARGE");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-03")]
    [Trait("AC", "AC-04")]
    public void AddMedia_UnsupportedImageFormat_ThrowsUnsupportedFormat()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        var file = new NewsMediaFile("key", 100, "application/zip");

        var act = () => article.AddMedia(Guid.NewGuid(), NewsMediaType.Image, file, "alt", Admin);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.NewsMediaUnsupportedFormat);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-01")]
    public void Publish_FromDraft_TransitionsAndRaisesEvent()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        var categoryId = Guid.NewGuid();

        article.Publish(Admin, At.AddMinutes(1), new[] { categoryId });

        article.Status.Should().Be(NewsStatus.Published);
        article.PublishedAtUtc.Should().Be(At.AddMinutes(1));
        var evt = article.DomainEvents.OfType<NewsArticlePublishedDomainEvent>().Should().ContainSingle().Which;
        evt.CategoryIds.Should().ContainSingle().Which.Should().Be(categoryId);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-02")]
    public void Publish_Twice_IsIdempotent_NoSecondEvent()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        article.Publish(Admin, At, Array.Empty<Guid>());
        article.ClearDomainEvents();

        article.Publish(Admin, At.AddMinutes(5), Array.Empty<Guid>());

        article.DomainEvents.Should().BeEmpty();
        article.PublishedAtUtc.Should().Be(At);
    }

    [Fact]
    public void Publish_WithoutAltTextOnImage_ThrowsNoAltText()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        article.AddMedia(Guid.NewGuid(), NewsMediaType.Image, new NewsMediaFile("key", 100, "image/png"), null, Admin);

        var act = () => article.Publish(Admin, At, Array.Empty<Guid>());

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.NewsMediaNoAltText);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-06")]
    [Trait("AC", "AC-01")]
    public void Archive_FromPublished_TransitionsAndRaisesEvent()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        article.Publish(Admin, At, Array.Empty<Guid>());
        article.ClearDomainEvents();

        article.Archive(Admin, At.AddDays(1));

        article.Status.Should().Be(NewsStatus.Archived);
        article.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<NewsArticleArchivedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-06")]
    [Trait("AC", "AC-02")]
    public void Archive_Twice_IsIdempotent_NoSecondEvent()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        article.Publish(Admin, At, Array.Empty<Guid>());
        article.Archive(Admin, At.AddDays(1));
        article.ClearDomainEvents();

        article.Archive(Admin, At.AddDays(2));

        article.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Archive_FromDraft_ThrowsMustBePublished()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);

        var act = () => article.Archive(Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.NewsMustBePublishedToArchive);
    }

    [Fact]
    public void Publish_FromArchived_ThrowsMustBeDraft()
    {
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, Title(), Body(), Admin, At);
        article.Publish(Admin, At, Array.Empty<Guid>());
        article.Archive(Admin, At.AddDays(1));

        var act = () => article.Publish(Admin, At.AddDays(2), Array.Empty<Guid>());

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.NewsMustBeDraftToPublish);
    }
}
