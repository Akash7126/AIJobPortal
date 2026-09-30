using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Interfaces;
using JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class ListApiVersionsHandler : IQueryHandler<ListApiVersionsQuery, IReadOnlyList<ApiVersionView>>
{
    private readonly IExternalIntegrationReadStore _store;

    public ListApiVersionsHandler(IExternalIntegrationReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<ApiVersionView>>> Handle(ListApiVersionsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListApiVersionsAsync(ct));
}
