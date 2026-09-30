namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface INewsArticleRepository
{
    Task<NewsArticle?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>INV-02: looks up an existing draft by its content hash before a new one is created.</summary>
    Task<NewsArticle?> GetDraftByContentHashAsync(string contentHash, CancellationToken ct = default);

    void Add(NewsArticle article);
}
