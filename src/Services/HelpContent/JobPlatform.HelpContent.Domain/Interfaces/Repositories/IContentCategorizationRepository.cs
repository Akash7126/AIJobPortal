namespace JobPlatform.HelpContent.Domain.Interfaces.Repositories;

public interface IContentCategorizationRepository
{
    Task<ContentCategorization?> GetByArticleAsync(Guid articleId, CancellationToken ct = default);

    void Add(ContentCategorization categorization);
}
