using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class HelpContentRepository(HelpContentDbContext db) : IHelpContentRepository
{
    public Task<Domain.HelpContent?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.HelpContents.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(Domain.HelpContent content) => db.HelpContents.Add(content);
}
