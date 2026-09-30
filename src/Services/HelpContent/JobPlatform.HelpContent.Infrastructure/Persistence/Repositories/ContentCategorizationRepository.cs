using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class ContentCategorizationRepository(HelpContentDbContext db) : IContentCategorizationRepository
{
    public Task<ContentCategorization?> GetByArticleAsync(Guid articleId, CancellationToken ct = default) =>
        db.ContentCategorizations.FirstOrDefaultAsync(c => c.ArticleId == articleId, ct);

    public void Add(ContentCategorization categorization) => db.ContentCategorizations.Add(categorization);
}
