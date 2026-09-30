using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Application.Queries.Connections;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Connections;

internal sealed class GetGovernmentSystemsHandler : IQueryHandler<GetGovernmentSystemsQuery, IReadOnlyList<GovernmentSourceConnectionView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetGovernmentSystemsHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<GovernmentSourceConnectionView>>> Handle(GetGovernmentSystemsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListGovernmentSourceConnectionsAsync(ct));
}
