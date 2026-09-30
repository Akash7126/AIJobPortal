using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class SoftwareInterfaceRepository(ExternalIntegrationDbContext db) : ISoftwareInterfaceRepository
{
    public Task<SoftwareInterfaceConnection?> GetByKeyAsync(SoftwareInterfaceCategory category, string name, CancellationToken ct = default) =>
        db.SoftwareInterfaces.FirstOrDefaultAsync(c => c.Category == category && c.Name == name, ct);

    public async Task<IReadOnlyList<SoftwareInterfaceConnection>> ListAsync(CancellationToken ct = default) =>
        await db.SoftwareInterfaces.ToListAsync(ct);

    public void Add(SoftwareInterfaceConnection connection) => db.SoftwareInterfaces.Add(connection);
}
