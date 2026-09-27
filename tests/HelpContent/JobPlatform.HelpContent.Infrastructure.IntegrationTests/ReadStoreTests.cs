using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Infrastructure.Adapters;
using JobPlatform.HelpContent.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.Extensions.Configuration;

namespace JobPlatform.HelpContent.Infrastructure.IntegrationTests;

public class ReadStoreTests
{
    private static HelpContentReadStore Store(HelpContentDbContext db) => new(db, new LocalMediaStorage(new ConfigurationBuilder().Build()));

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    [Trait("AC", "AC-02")]
    public async Task ListNews_DeletedCategory_FallsBackToUncategorized()
    {
        await using var database = Db.New();
        Guid articleId;
        await using (var write = database.NewContext())
        {
            var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0);
            article.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
            articleId = article.Id;
            write.NewsArticles.Add(article);
            var category = ContentCategory.Create(Guid.NewGuid(), new(null, "Announcements"), Db.Admin);
            category.SoftDelete(Db.Admin);
            write.ContentCategories.Add(category);
            var categorization = ContentCategorization.CreateFor(articleId);
            categorization.Assign(new[] { category.Id }, Array.Empty<string>(), Db.Admin);
            write.ContentCategorizations.Add(categorization);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var page = await Store(read).ListNewsAsync(null, "Published", new PageRequest(1, 10));

        page.Items.Should().ContainSingle(i => i.NewsArticleId == articleId).Which.CategoryNames.Should().Contain("Uncategorized");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    public async Task ListNews_FilterByCategory_OnlyReturnsMatchingArticles()
    {
        await using var database = Db.New();
        var categoryId = Guid.NewGuid();
        Guid matchingId, otherId;
        await using (var write = database.NewContext())
        {
            var matching = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "Match"), new(null, "B"), Db.Admin, Db.T0);
            matching.Publish(Db.Admin, Db.T0, new[] { categoryId });
            matchingId = matching.Id;
            var other = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "Other"), new(null, "B"), Db.Admin, Db.T0);
            other.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
            otherId = other.Id;
            write.NewsArticles.AddRange(matching, other);
            var categorization = ContentCategorization.CreateFor(matchingId);
            categorization.Assign(new[] { categoryId }, Array.Empty<string>(), Db.Admin);
            write.ContentCategorizations.Add(categorization);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var page = await Store(read).ListNewsAsync(categoryId, "Published", new PageRequest(1, 10));

        page.Items.Should().ContainSingle(i => i.NewsArticleId == matchingId);
        page.Items.Should().NotContain(i => i.NewsArticleId == otherId);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-06")]
    [Trait("AC", "AC-05")]
    public async Task SearchNewsArchive_ArchivedArticleIsStillSearchable()
    {
        await using var database = Db.New();
        Guid articleId;
        await using (var write = database.NewContext())
        {
            var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "Historic policy change"), new(null, "B"), Db.Admin, Db.T0);
            article.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
            article.Archive(Db.Admin, Db.T0.AddDays(1));
            articleId = article.Id;
            write.NewsArticles.Add(article);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var page = await Store(read).SearchNewsArchiveAsync("historic", new PageRequest(1, 10));

        page.Items.Should().ContainSingle(i => i.NewsArticleId == articleId);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-05")]
    public async Task GetPersonalizedFeed_MatchesByTag()
    {
        await using var database = Db.New();
        Guid articleId;
        await using (var write = database.NewContext())
        {
            var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0);
            article.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
            articleId = article.Id;
            write.NewsArticles.Add(article);
            var categorization = ContentCategorization.CreateFor(articleId);
            categorization.Assign(Array.Empty<Guid>(), new[] { "software" }, Db.Admin);
            write.ContentCategorizations.Add(categorization);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var feed = await Store(read).GetPersonalizedFeedAsync(new[] { "Software" }, 20);

        feed.Should().ContainSingle(i => i.NewsArticleId == articleId);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    [Trait("AC", "AC-04")]
    public async Task GetHelpCenter_FiltersByAssignedRole()
    {
        await using var database = Db.New();
        Guid jobSeekerContentId, employerContentId;
        await using (var write = database.NewContext())
        {
            var jobSeekerContent = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "For job seekers"), new(null, "B"), Db.Admin, Db.T0);
            var employerContent = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "For employers"), new(null, "B"), Db.Admin, Db.T0);
            jobSeekerContentId = jobSeekerContent.Id;
            employerContentId = employerContent.Id;
            write.HelpContents.AddRange(jobSeekerContent, employerContent);
            var org1 = HelpContentOrganization.CreateFor(jobSeekerContentId);
            org1.AssignTopicAndRoles(null, new[] { HelpRole.JobSeeker }, Db.Admin);
            var org2 = HelpContentOrganization.CreateFor(employerContentId);
            org2.AssignTopicAndRoles(null, new[] { HelpRole.Employer }, Db.Admin);
            write.HelpContentOrganizations.AddRange(org1, org2);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var center = await Store(read).GetHelpCenterAsync(HelpRole.JobSeeker);

        var ids = center.SelectMany(t => t.Items).Select(i => i.HelpContentId).ToArray();
        ids.Should().Contain(jobSeekerContentId);
        ids.Should().NotContain(employerContentId);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-01")]
    [Trait("AC", "AC-02")]
    public async Task SearchHelpContent_NoResults_ReturnsEmptyPage_NotAnError()
    {
        await using var database = Db.New();
        await using var read = database.NewContext();

        var result = await Store(read).SearchHelpContentAsync("nonexistent-keyword", null, new PageRequest(1, 10));

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-03")]
    public async Task GetFeedbackSummary_CountsHelpfulAndNotHelpful()
    {
        await using var database = Db.New();
        var contentId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.HelpFeedbacks.Add(HelpFeedback.Submit(Guid.NewGuid(), contentId, Guid.NewGuid(), FeedbackRating.Helpful, null, Db.T0));
            write.HelpFeedbacks.Add(HelpFeedback.Submit(Guid.NewGuid(), contentId, Guid.NewGuid(), FeedbackRating.Helpful, null, Db.T0));
            write.HelpFeedbacks.Add(HelpFeedback.Submit(Guid.NewGuid(), contentId, Guid.NewGuid(), FeedbackRating.NotHelpful, null, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var summary = await Store(read).GetFeedbackSummaryAsync(contentId);

        summary!.HelpfulCount.Should().Be(2);
        summary.NotHelpfulCount.Should().Be(1);
    }
}
