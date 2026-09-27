using JobPlatform.ExternalIntegration.Application;

namespace JobPlatform.ExternalIntegration.Infrastructure.Adapters;

/// <summary>
/// Default adapter for <see cref="IPartnerJobFeedClient"/> (handover 4.3, anti-corruption layer): since no real partner sandbox is reachable
/// from this environment (no Docker/network egress - see the BC-02 status doc "Known limitations"), pull always returns an empty page. A
/// production deployment replaces this with an HttpPartnerJobFeedClient (Polly: 30s timeout, 3 retries with jitter, then
/// E-EJSI-UPSTREAM-TIMEOUT) without any Application-layer change, since both sit behind the same port.
/// </summary>
internal sealed class FakePartnerJobFeedClient : IPartnerJobFeedClient
{
    public Task<IReadOnlyList<PartnerJobPayload>> FetchAsync(Guid sourcePlatformId, string baseUrl, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PartnerJobPayload>>(Array.Empty<PartnerJobPayload>());
}
