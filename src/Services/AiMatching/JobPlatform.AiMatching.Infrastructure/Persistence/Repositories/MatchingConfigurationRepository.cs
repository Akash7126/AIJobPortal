using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

// Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).

internal sealed class MatchingConfigurationRepository(AiMatchingDbContext db) : IMatchingConfigurationRepository
{
    public Task<MatchingConfiguration?> GetCurrentAsync(CancellationToken ct = default) =>
        db.MatchingConfigurations.Include(c => c.History).FirstOrDefaultAsync(c => c.Id == MatchingConfiguration.SingletonId, ct);

    public void Add(MatchingConfiguration configuration) => db.MatchingConfigurations.Add(configuration);
}
