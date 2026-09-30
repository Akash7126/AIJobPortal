using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class NewsArticleRepository(HelpContentDbContext db) : INewsArticleRepository
{
    public Task<NewsArticle?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.NewsArticles.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<NewsArticle?> GetDraftByContentHashAsync(string contentHash, CancellationToken ct = default) =>
        db.NewsArticles.FirstOrDefaultAsync(a => a.ContentHash == contentHash && a.Status == NewsStatus.Draft, ct);

    public void Add(NewsArticle article) => db.NewsArticles.Add(article);
}
