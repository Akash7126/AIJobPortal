using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.ApiCredentials;

internal sealed class GetCurrentApiCredentialHandler : IQueryHandler<GetCurrentApiCredentialQuery, ApiCredentialView>
{
    private readonly IIdentityReadStore _store;
    private readonly ICurrentUser _user;

    public GetCurrentApiCredentialHandler(IIdentityReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<ApiCredentialView>> Handle(GetCurrentApiCredentialQuery request, CancellationToken ct)
    {
        var view = await _store.GetActiveApiCredentialForPartnerAsync(_user.UserId!.Value, ct);
        return view is null ? Error.NotFound("E-API-CREDENTIAL-NOT-FOUND", "The partner has no active API credential.") : view;
    }
}
