using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.HelpContent.Infrastructure.Persistence.Repositories;

internal sealed class ContextHelpMappingRepository(HelpContentDbContext db) : IContextHelpMappingRepository
{
    public Task<ContextHelpMapping?> GetByPageKeyAsync(string pageKey, CancellationToken ct = default) =>
        db.ContextHelpMappings.FirstOrDefaultAsync(m => m.PageKey == pageKey, ct);

    public void Add(ContextHelpMapping mapping) => db.ContextHelpMappings.Add(mapping);
}
