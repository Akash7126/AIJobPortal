using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Queries.Consent;
using JobPlatform.AccountIdentity.Application.Services.Consent;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Application.Handlers.Consent;

internal sealed class GetCurrentConsentHandler : IQueryHandler<GetCurrentConsentQuery, ConsentStatusDto>
{
    private readonly IIdentityReadStore _store;
    private readonly ConsentOptions _options;
    private readonly ConsentService _consentService;

    public GetCurrentConsentHandler(IIdentityReadStore store, IOptions<ConsentOptions> options, ConsentService consentService)
    {
        _store = store;
        _options = options.Value;
        _consentService = consentService;
    }

    public async Task<Result<ConsentStatusDto>> Handle(GetCurrentConsentQuery request, CancellationToken ct)
    {
        if (request.GuestId is not { } guestId)
        {
            return _consentService.ToStatus(null, null);
        }

        return _consentService.ToStatus(guestId, await _store.GetConsentAsync(guestId, _options.CurrentPolicyVersion, ct));
    }
}
