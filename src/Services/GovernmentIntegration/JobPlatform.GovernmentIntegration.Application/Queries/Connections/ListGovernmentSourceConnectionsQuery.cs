using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;

namespace JobPlatform.GovernmentIntegration.Application.Queries.Connections;

public sealed record ListGovernmentSourceConnectionsQuery : AdminQuery<IReadOnlyList<GovernmentSourceConnectionView>>;
