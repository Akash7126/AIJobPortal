using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class MigrationRunRepository(GovernmentIntegrationDbContext db) : IMigrationRunRepository
{
    private static readonly MigrationStatus[] ActiveStatuses = { MigrationStatus.Created, MigrationStatus.Running, MigrationStatus.PhaseFailed };

    public Task<MigrationRun?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.MigrationRuns.Include(r => r.Phases).Include(r => r.Log).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<MigrationRun?> GetActiveAsync(CancellationToken ct = default) =>
        db.MigrationRuns.Include(r => r.Phases).Include(r => r.Log).FirstOrDefaultAsync(r => ActiveStatuses.Contains(r.Status), ct);

    public void Add(MigrationRun run) => db.MigrationRuns.Add(run);
}
