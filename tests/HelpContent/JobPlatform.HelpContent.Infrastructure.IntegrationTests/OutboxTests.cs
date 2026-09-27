using System.Text.Json.Nodes;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.HelpContent.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.IntegrationTests;

/// <summary>Handover section 10: outbox rows for the four published events, and none on a no-op double publish/archive.</summary>
public class OutboxTests
{
    [Fact]
    [Trait("Story", "US-3.7.1-02")]
    public async Task CreateDraft_WritesOneOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.NewsArticles.Add(NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0));

        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        (row.Type, row.Exchange, row.RoutingKey).Should().Be(("NewsArticleCreated", "jobplatform.help-content.events", "news-article.created.v1"));
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Publish_WritesOutboxRow_WithCategoryIds()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0);
        db.NewsArticles.Add(article);
        await db.SaveChangesAsync();

        var categoryId = Guid.NewGuid();
        article.Publish(Db.Admin, Db.T0.AddMinutes(1), new[] { categoryId });
        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().Where(m => m.Type == "NewsArticlePublished").SingleAsync();
        row.RoutingKey.Should().Be("news-article.published.v1");
        var payload = JsonNode.Parse(row.Payload)!;
        payload["categoryIds"]!.AsArray().Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-01")]
    [Trait("AC", "AC-02")]
    public async Task Publish_Twice_WritesOnlyOneOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0);
        db.NewsArticles.Add(article);
        await db.SaveChangesAsync();

        article.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
        await db.SaveChangesAsync();
        article.Publish(Db.Admin, Db.T0.AddMinutes(5), Array.Empty<Guid>());
        await db.SaveChangesAsync();

        (await db.Set<OutboxMessage>().CountAsync(m => m.Type == "NewsArticlePublished")).Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.7.1-06")]
    [Trait("AC", "AC-05")]
    public async Task Archive_Twice_WritesOnlyOneOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var article = NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0);
        db.NewsArticles.Add(article);
        article.Publish(Db.Admin, Db.T0, Array.Empty<Guid>());
        await db.SaveChangesAsync();

        article.Archive(Db.Admin, Db.T0.AddDays(1));
        await db.SaveChangesAsync();
        article.Archive(Db.Admin, Db.T0.AddDays(2));
        await db.SaveChangesAsync();

        (await db.Set<OutboxMessage>().CountAsync(m => m.Type == "NewsArticleArchived")).Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    public async Task Update_WritesOutboxRow_WithFromAndToVersion()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q1"), new(null, "A1"), Db.Admin, Db.T0);
        db.HelpContents.Add(content);
        await db.SaveChangesAsync();

        content.Update(new(null, "Q2"), new(null, "A2"), Db.Admin, Db.T0.AddMinutes(1));
        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().Where(m => m.Type == "HelpContentUpdated").SingleAsync();
        row.RoutingKey.Should().Be("help-content.updated.v1");
        var payload = JsonNode.Parse(row.Payload)!;
        payload["fromVersion"]!.GetValue<int>().Should().Be(1);
        payload["toVersion"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        await db.BeginTransactionAsync();
        db.NewsArticles.Add(NewsArticle.CreateDraft(Guid.NewGuid(), NewsKind.Article, new(null, "T"), new(null, "B"), Db.Admin, Db.T0));
        await db.SaveChangesAsync();
        await db.RollbackTransactionAsync();

        (await db.NewsArticles.CountAsync()).Should().Be(0);
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }
}
