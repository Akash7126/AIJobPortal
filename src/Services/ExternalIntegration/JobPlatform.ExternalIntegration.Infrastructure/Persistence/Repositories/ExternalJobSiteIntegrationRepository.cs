using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class ExternalJobSiteIntegrationRepository(ExternalIntegrationDbContext db) : IExternalJobSiteIntegrationRepository
{
    public Task<ExternalJobSiteIntegration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Integrations.Include(i => i.SyncRuns).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<ExternalJobSiteIntegration?> GetByPartnerAccountAsync(Guid partnerAccountId, CancellationToken ct = default) =>
        db.Integrations.Include(i => i.SyncRuns).FirstOrDefaultAsync(i => i.PartnerAccountId == partnerAccountId, ct);

    public Task<ExternalJobSiteIntegration?> GetBySourcePlatformAsync(Guid sourcePlatformId, CancellationToken ct = default) =>
        db.Integrations.Include(i => i.SyncRuns).FirstOrDefaultAsync(i => i.SourcePlatform.Id == sourcePlatformId, ct);

    public void Add(ExternalJobSiteIntegration integration) => db.Integrations.Add(integration);
}
