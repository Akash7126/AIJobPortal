using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Application.Queries.Connections;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Connections;

internal sealed class ListGovernmentSourceConnectionsHandler : IQueryHandler<ListGovernmentSourceConnectionsQuery, IReadOnlyList<GovernmentSourceConnectionView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public ListGovernmentSourceConnectionsHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<GovernmentSourceConnectionView>>> Handle(ListGovernmentSourceConnectionsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListGovernmentSourceConnectionsAsync(ct));
}
