using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IExternalIntegrationReadStore
{
    Task<IntegrationView?> GetIntegrationByPartnerAsync(Guid partnerAccountId, CancellationToken ct = default);

    Task<IntegrationSummaryView?> GetIntegrationSummaryAsync(Guid sourcePlatformId, CancellationToken ct = default);

    Task<IReadOnlyList<ApiVersionView>> ListApiVersionsAsync(CancellationToken ct = default);

    Task<ApiVersionView?> GetApiVersionAsync(string version, CancellationToken ct = default);

    Task<IReadOnlyList<SoftwareInterfaceView>> ListSoftwareInterfacesAsync(CancellationToken ct = default);
}
