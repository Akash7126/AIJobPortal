using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class GovernmentSourceConnectionRepository(GovernmentIntegrationDbContext db) : IGovernmentSourceConnectionRepository
{
    public Task<GovernmentSourceConnection?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.GovernmentSourceConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<GovernmentSourceConnection?> GetBySourceAsync(SourceSystem source, CancellationToken ct = default) =>
        db.GovernmentSourceConnections.FirstOrDefaultAsync(c => c.Source == source, ct);

    public async Task<IReadOnlyList<GovernmentSourceConnection>> ListAsync(CancellationToken ct = default) =>
        await db.GovernmentSourceConnections.OrderBy(c => c.Source).ToListAsync(ct);

    public void Add(GovernmentSourceConnection connection) => db.GovernmentSourceConnections.Add(connection);
}
