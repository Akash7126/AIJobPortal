using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class GetAccountSummaryHandler : IQueryHandler<GetAccountSummaryQuery, AccountSummaryDto>
{
    private readonly IIdentityReadStore _store;

    public GetAccountSummaryHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<AccountSummaryDto>> Handle(GetAccountSummaryQuery request, CancellationToken ct)
    {
        var summary = await _store.GetAccountSummaryAsync(request.AccountId, ct);
        return summary is null ? AccountErrors.NotFound : summary;
    }
}
