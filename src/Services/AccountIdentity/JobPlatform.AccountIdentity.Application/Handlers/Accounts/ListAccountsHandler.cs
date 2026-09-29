using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class ListAccountsHandler : IQueryHandler<ListAccountsQuery, PagedResult<AccountListItemView>>
{
    private readonly IIdentityReadStore _store;

    public ListAccountsHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<PagedResult<AccountListItemView>>> Handle(ListAccountsQuery request, CancellationToken ct)
    {
        var page = await _store.ListAccountsAsync(new AccountListFilter(request.ActorType, request.Standing, request.Search),
            new PageRequest(request.Page, request.PageSize), ct);
        return page with { Items = page.Items.Select(i => i with { Email = Masking.Email(i.Email), Mobile = Masking.Mobile(i.Mobile) }).ToList() };
    }
}
