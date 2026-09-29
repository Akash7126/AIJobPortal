using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Internal;

internal sealed class ListAccessLogHandler : IQueryHandler<ListAccessLogQuery, PagedResult<AccessLogEntryDto>>
{
    private readonly IIdentityReadStore _store;

    public ListAccessLogHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<PagedResult<AccessLogEntryDto>>> Handle(ListAccessLogQuery request, CancellationToken ct) =>
        await _store.ListAccessLogAsync(request.FromUtc, request.ToUtc, request.AccountId, new PageRequest(request.Page, request.PageSize), ct);
}
