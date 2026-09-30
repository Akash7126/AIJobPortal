using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class ContentCategoryRepository(HelpContentDbContext db) : IContentCategoryRepository
{
    public Task<ContentCategory?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.ContentCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ContentCategory>> ListAsync(CancellationToken ct = default) => await db.ContentCategories.ToListAsync(ct);

    public void Add(ContentCategory category) => db.ContentCategories.Add(category);
}
