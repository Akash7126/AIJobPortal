using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;

namespace JobPlatform.GovernmentIntegration.Application.Queries.Connections;

/// <summary>F-0001 (US-4.3-01): BC-02 reads the connection catalogue and health synchronously rather than via an event (handover Q-06:
/// left open in the source's favour - no ConnectionHealthChanged event is published).</summary>
public sealed record GetGovernmentSystemsQuery : ServiceQuery<IReadOnlyList<GovernmentSourceConnectionView>>;
