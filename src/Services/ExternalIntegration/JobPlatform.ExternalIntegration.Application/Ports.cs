using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

namespace JobPlatform.ExternalIntegration.Application;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IExternalIntegrationReadStore
{
    Task<IntegrationView?> GetIntegrationByPartnerAsync(Guid partnerAccountId, CancellationToken ct = default);

    Task<IntegrationSummaryView?> GetIntegrationSummaryAsync(Guid sourcePlatformId, CancellationToken ct = default);

    Task<IReadOnlyList<ApiVersionView>> ListApiVersionsAsync(CancellationToken ct = default);

    Task<ApiVersionView?> GetApiVersionAsync(string version, CancellationToken ct = default);

    Task<IReadOnlyList<SoftwareInterfaceView>> ListSoftwareInterfacesAsync(CancellationToken ct = default);
}

/// <summary>One partner job as returned by the pull feed, in the partner's own (non-standard) shape: raw field name/value pairs a
/// JobDataMapping translates. Partner shapes never leave this adapter boundary (handover 4.3, anti-corruption layer).</summary>
public sealed record PartnerJobPayload(string SourceJobId, IReadOnlyDictionary<string, string> Fields);

/// <summary>Anti-corruption layer port (handover 4.3): fetches a partner's job feed for the pull model. The default adapter is a
/// configurable fake (no real partner sandbox is reachable in this environment - see BC-02 status doc "Known limitations").</summary>
public interface IPartnerJobFeedClient
{
    Task<IReadOnlyList<PartnerJobPayload>> FetchAsync(Guid sourcePlatformId, string baseUrl, CancellationToken ct = default);
}
