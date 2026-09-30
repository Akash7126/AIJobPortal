using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Application.Queries.Migration;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class ListGovernmentExchangesHandler :
    IQueryHandler<ListGovernmentExchangesQuery, SharedKernel.Application.Paging.PagedResult<GovernmentAccessLogView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public ListGovernmentExchangesHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<SharedKernel.Application.Paging.PagedResult<GovernmentAccessLogView>>> Handle(ListGovernmentExchangesQuery request,
        CancellationToken ct) =>
        await _store.ListGovernmentExchangesAsync(request.From, request.To, new SharedKernel.Application.Paging.PageRequest(request.Page, request.PageSize), ct);
}
