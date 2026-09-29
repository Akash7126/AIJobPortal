using JobPlatform.PlatformAdministration.Application.DTOs.Offerings;
using JobPlatform.PlatformAdministration.Application.Queries.Offerings;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Offerings;

internal sealed class ListJobOfferingsHandler : IQueryHandler<ListJobOfferingsQuery, PagedResult<JobOfferingListItem>>
{
    private readonly IAdminReadStore _store;

    public ListJobOfferingsHandler(IAdminReadStore store) => _store = store;

    public async Task<Result<PagedResult<JobOfferingListItem>>> Handle(ListJobOfferingsQuery request, CancellationToken ct) =>
        await _store.ListJobOfferingsAsync(request.Status, new PageRequest(request.Page, request.PageSize), ct);
}
