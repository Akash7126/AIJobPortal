namespace JobPlatform.ExternalIntegration.Application.Interfaces;

/// <summary>Anti-corruption layer port (handover 4.3): fetches a partner's job feed for the pull model. The default adapter is a
/// configurable fake (no real partner sandbox is reachable in this environment - see BC-02 status doc "Known limitations").</summary>
public interface IPartnerJobFeedClient
{
    Task<IReadOnlyList<PartnerJobPayload>> FetchAsync(Guid sourcePlatformId, string baseUrl, CancellationToken ct = default);
}
