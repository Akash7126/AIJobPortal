using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.ApiCredentials;

internal sealed class GetApiCredentialControlsHandler : IQueryHandler<GetApiCredentialControlsQuery, ApiCredentialControlsDto>
{
    private readonly IIdentityReadStore _store;

    public GetApiCredentialControlsHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<ApiCredentialControlsDto>> Handle(GetApiCredentialControlsQuery request, CancellationToken ct)
    {
        var view = await _store.GetApiCredentialControlsAsync(request.ApiCredentialId, ct);
        return view is null ? Error.NotFound("E-API-CREDENTIAL-NOT-FOUND", "The API credential was not found.") : view;
    }
}
