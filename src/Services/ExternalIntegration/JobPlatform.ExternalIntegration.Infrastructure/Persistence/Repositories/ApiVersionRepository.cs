using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class ApiVersionRepository(ExternalIntegrationDbContext db) : IApiVersionRepository
{
    public Task<ApiVersion?> GetAsync(string version, CancellationToken ct = default) => db.ApiVersions.FirstOrDefaultAsync(v => v.Id == version, ct);

    public async Task<IReadOnlyList<ApiVersion>> ListAsync(CancellationToken ct = default) => await db.ApiVersions.ToListAsync(ct);

    public void Add(ApiVersion version) => db.ApiVersions.Add(version);
}
