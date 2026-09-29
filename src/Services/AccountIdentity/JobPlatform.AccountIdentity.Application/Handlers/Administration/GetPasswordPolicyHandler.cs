using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.AccountIdentity.Application.Queries.Administration;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class GetPasswordPolicyHandler : IQueryHandler<GetPasswordPolicyQuery, PasswordPolicyView>
{
    private readonly IIdentityReadStore _store;

    public GetPasswordPolicyHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<PasswordPolicyView>> Handle(GetPasswordPolicyQuery request, CancellationToken ct) =>
        await _store.GetPasswordPolicyAsync(ct);
}
