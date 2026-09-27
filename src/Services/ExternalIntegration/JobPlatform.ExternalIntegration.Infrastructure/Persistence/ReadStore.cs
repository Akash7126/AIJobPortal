using JobPlatform.ExternalIntegration.Application;
using JobPlatform.ExternalIntegration.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence;

internal sealed class ExternalIntegrationReadStore(ExternalIntegrationDbContext db) : IExternalIntegrationReadStore
{
    public async Task<IntegrationView?> GetIntegrationByPartnerAsync(Guid partnerAccountId, CancellationToken ct = default)
    {
        var integration = await db.Integrations.AsNoTracking().Include(i => i.SyncRuns)
            .FirstOrDefaultAsync(i => i.PartnerAccountId == partnerAccountId, ct);
        return integration is null ? null : ToView(integration);
    }

    public async Task<IntegrationSummaryView?> GetIntegrationSummaryAsync(Guid sourcePlatformId, CancellationToken ct = default)
    {
        var integration = await db.Integrations.AsNoTracking().Include(i => i.SyncRuns)
            .FirstOrDefaultAsync(i => i.SourcePlatform.Id == sourcePlatformId, ct);
        if (integration is null)
        {
            return null;
        }

        var recentRuns = integration.SyncRuns.OrderByDescending(r => r.StartedAtUtc).Take(10).Select(ToView).ToArray();
        return new IntegrationSummaryView(integration.Id, integration.SourcePlatform.Id, integration.SourcePlatform.Name,
            integration.Status.ToString(), integration.AttributionVisibility.ToString(), recentRuns);
    }

    private static IntegrationView ToView(ExternalJobSiteIntegration i) => new(
        i.Id, i.PartnerAccountId, i.SourcePlatform.Id, i.SourcePlatform.Name, i.SourcePlatform.BaseUrl, i.AdmissionStatus.ToString(),
        i.Recommendation.ToString(), i.Status.ToString(), i.Models.PullEnabled, i.Models.PushEnabled, i.Schedule.Mode.ToString(), i.Schedule.Cron,
        i.AttributionVisibility.ToString(), i.Sandbox.ToString(), i.SyncRuns.Select(ToView).ToArray(), i.RowVersion);

    private static SyncRunView ToView(SyncRun r) => new(
        r.Id, r.Trigger.ToString(), r.Status.ToString(), r.StartedAtUtc, r.EndedAtUtc, r.MappingVersion, r.Received, r.Accepted, r.Rejected,
        r.ErrorCode);

    public async Task<IReadOnlyList<ApiVersionView>> ListApiVersionsAsync(CancellationToken ct = default) =>
        await db.ApiVersions.AsNoTracking()
            .Select(v => new ApiVersionView(v.Id, v.Status.ToString(), v.DeprecatedAtUtc, v.SunsetAtUtc, v.AcceptedFormats))
            .ToListAsync(ct);

    public async Task<ApiVersionView?> GetApiVersionAsync(string version, CancellationToken ct = default)
    {
        var v = await db.ApiVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == version, ct);
        return v is null ? null : new ApiVersionView(v.Id, v.Status.ToString(), v.DeprecatedAtUtc, v.SunsetAtUtc, v.AcceptedFormats);
    }

    public async Task<IReadOnlyList<SoftwareInterfaceView>> ListSoftwareInterfacesAsync(CancellationToken ct = default) =>
        await db.SoftwareInterfaces.AsNoTracking().Where(c => c.Enabled)
            .Select(c => new SoftwareInterfaceView(c.Id, c.Category.ToString(), c.Name, c.Endpoint, c.Enabled))
            .ToListAsync(ct);
}
