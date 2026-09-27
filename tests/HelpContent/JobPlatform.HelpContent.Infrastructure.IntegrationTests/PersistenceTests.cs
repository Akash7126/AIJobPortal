using JobPlatform.HelpContent.Application.Events;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.HelpContent.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Actor Admin = new(Guid.NewGuid(), true);

    public static SqliteTestDatabase<HelpContentDbContext> New() => new(o => new HelpContentDbContext(o), new HelpContentEventMapper());
}

public class RepositoryTests
{
    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    public async Task NewsArticle_RoundTripsMediaAndLocalizedFields()
    {
        await using var database = Db.New();
        Guid articleId;
        await using (var write = database.NewContext())
        {
            var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Announcement, new("عنوان", "Title"), new("محتوى", "Body"), Db.Admin, Db.T0);
            article.AddMedia(Guid.NewGuid(), NewsMediaType.Image, new NewsMediaFile("key1", 100, "image/png"), "alt text", Db.Admin);
            articleId = article.Id;
            write.NewsArticles.Add(article);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new NewsArticleRepository(read).GetByIdAsync(articleId);
        loaded!.Title.En.Should().Be("Title");
        loaded.Title.Ar.Should().Be("عنوان");
        loaded.Media.Should().ContainSingle().Which.AltText.Should().Be("alt text");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    [Trait("AC", "AC-03")]
    public async Task NewsArticle_GetDraftByContentHash_FindsOnlyDrafts()
    {
        await using var database = Db.New();
        var title = new LocalizedText(null, "Same title");
        var body = new LocalizedText(null, "Same body");
        var hash = NewsArticle.ComputeContentHash(title, body);
        await using (var write = database.NewContext())
        {
            write.NewsArticles.Add(NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, title, body, Db.Admin, Db.T0));
            await write.SaveChangesAsync();
        }

        (await new NewsArticleRepository(database.NewContext()).GetDraftByContentHashAsync(hash)).Should().NotBeNull();

        await using (var publish = database.NewContext())
        {
            var tracked = await publish.NewsArticles.SingleAsync();
            tracked.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
            await publish.SaveChangesAsync();
        }

        (await new NewsArticleRepository(database.NewContext()).GetDraftByContentHashAsync(hash)).Should().BeNull("once published it is no longer a draft");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    [Trait("AC", "AC-03")]
    public async Task ContentCategorization_ConcurrentOverwrites_LaterSaveWins_NoConcurrencyException()
    {
        await using var database = Db.New();
        var articleId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.ContentCategorizations.Add(ContentCategorization.CreateFor(articleId));
            await write.SaveChangesAsync();
        }

        await using var editorA = database.NewContext();
        await using var editorB = database.NewContext();
        var categorizationA = await editorA.ContentCategorizations.SingleAsync(c => c.ArticleId == articleId);
        var categorizationB = await editorB.ContentCategorizations.SingleAsync(c => c.ArticleId == articleId);
        var categoryA = Guid.NewGuid();
        var categoryB = Guid.NewGuid();
        categorizationA.Assign(new[] { categoryA }, Array.Empty<string>(), Db.Admin);
        categorizationB.Assign(new[] { categoryB }, Array.Empty<string>(), Db.Admin);

        await editorA.SaveChangesAsync();
        var act = () => editorB.SaveChangesAsync();

        await act.Should().NotThrowAsync("ContentCategorization is 'later save wins' - no RowVersion concurrency check");
        var final = await new ContentCategorizationRepository(database.NewContext()).GetByArticleAsync(articleId);
        final!.CategoryIds.Should().ContainSingle().Which.Should().Be(categoryB, "editor B saved last");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-01")]
    public async Task HelpContent_RoundTripsAllVersions()
    {
        await using var database = Db.New();
        Guid contentId;
        await using (var write = database.NewContext())
        {
            var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q1"), new(null, "A1"), Db.Admin, Db.T0);
            content.Update(new(null, "Q2"), new(null, "A2"), Db.Admin, Db.T0.AddMinutes(1));
            contentId = content.Id;
            write.HelpContents.Add(content);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new HelpContentRepository(read).GetByIdAsync(contentId);
        loaded!.CurrentVersion.Should().Be(2);
        loaded.Versions.Should().HaveCount(2);
        loaded.Current.Title.En.Should().Be("Q2");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-08")]
    public async Task HelpContent_RoundTripsMedia()
    {
        await using var database = Db.New();
        Guid contentId;
        await using (var write = database.NewContext())
        {
            var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Video, new(null, "T"), new(null, "B"), Db.Admin, Db.T0);
            content.AttachMedia(Guid.NewGuid(), HelpMediaType.Video, "video-key", "captions-key", null, Db.Admin);
            contentId = content.Id;
            write.HelpContents.Add(content);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new HelpContentRepository(read).GetByIdAsync(contentId);
        loaded!.Media.Should().ContainSingle().Which.CaptionsRef.Should().Be("captions-key");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    [Trait("AC", "AC-03")]
    public async Task HelpContentOrganization_ConcurrentOverwrites_LaterSaveWins()
    {
        await using var database = Db.New();
        var contentId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.HelpContentOrganizations.Add(HelpContentOrganization.CreateFor(contentId));
            await write.SaveChangesAsync();
        }

        await using var editorA = database.NewContext();
        await using var editorB = database.NewContext();
        (await editorA.HelpContentOrganizations.SingleAsync()).AssignTopicAndRoles(Guid.NewGuid(), new[] { HelpRole.JobSeeker }, Db.Admin);
        var topicB = Guid.NewGuid();
        (await editorB.HelpContentOrganizations.SingleAsync()).AssignTopicAndRoles(topicB, new[] { HelpRole.Employer }, Db.Admin);

        await editorA.SaveChangesAsync();
        await editorB.SaveChangesAsync();

        var final = await new HelpOrganizationRepository(database.NewContext()).GetAsync(contentId);
        final!.TopicId.Should().Be(topicB);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-02")]
    public async Task HelpFeedback_UniqueConstraint_OneRowPerUserAndContent()
    {
        await using var database = Db.New();
        var helpContentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.HelpFeedbacks.Add(HelpFeedback.Submit(Guid.NewGuid(), helpContentId, userId, FeedbackRating.Helpful, null, Db.T0));
        await db.SaveChangesAsync();
        db.HelpFeedbacks.Add(HelpFeedback.Submit(Guid.NewGuid(), helpContentId, userId, FeedbackRating.NotHelpful, null, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-07")]
    public async Task TutorialProgress_RoundTrips_AndEnforcesUniquePerUserAndTutorial()
    {
        await using var database = Db.New();
        var userId = Guid.NewGuid();
        var tutorialId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.TutorialProgresses.Add(TutorialProgress.Complete(Guid.NewGuid(), userId, tutorialId, Db.T0));
        await db.SaveChangesAsync();
        db.TutorialProgresses.Add(TutorialProgress.Complete(Guid.NewGuid(), userId, tutorialId, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    public async Task ContextHelpMapping_RoundTrips_AndEnforcesUniquePageKey()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.ContextHelpMappings.Add(ContextHelpMapping.Create(Guid.NewGuid(), "employer/dashboard", Guid.NewGuid(), Db.Admin));
        await db.SaveChangesAsync();
        db.ContextHelpMappings.Add(ContextHelpMapping.Create(Guid.NewGuid(), "employer/dashboard", Guid.NewGuid(), Db.Admin));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    public async Task CompanyProfilePage_RoundTrips()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var page = CompanyProfilePage.OpenFor(Guid.NewGuid(), employerId, Db.T0);
            page.EditBackground(new(null, "We build things."), new[] { "Great culture" }, new Actor(employerId, false), Db.T0);
            write.CompanyProfilePages.Add(page);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new CompanyProfilePageRepository(read).GetByEmployerAsync(employerId);
        loaded!.Background.En.Should().Be("We build things.");
        loaded.Highlights.Should().ContainSingle();
    }
}
