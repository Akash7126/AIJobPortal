using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Consent;
using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.AccountIdentity.Application.Services.Consent;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Application.Handlers.Consent;

internal sealed class RecordPrivacyConsentHandler : ICommandHandler<RecordPrivacyConsentCommand, ConsentStatusDto>
{
    private readonly IPrivacyConsentRepository _consents;
    private readonly TimeProvider _clock;
    private readonly ConsentService _consentService;

    public RecordPrivacyConsentHandler(IPrivacyConsentRepository consents, TimeProvider clock, ConsentService consentService)
    {
        _consents = consents;
        _clock = clock;
        _consentService = consentService;
    }

    public async Task<Result<ConsentStatusDto>> Handle(RecordPrivacyConsentCommand request, CancellationToken ct)
    {
        var guestId = request.GuestId ?? Guid.NewGuid();
        var choices = new ConsentChoices(request.Analytics, request.Preferences, request.Marketing);
        var locale = request.Locale == "ar" ? Language.Ar : Language.En;

        // Idempotent per guest + policy version: a second decision updates the existing row instead of adding one.
        var existing = await _consents.GetAsync(guestId, request.PolicyVersion, ct);
        PrivacyConsent consent;
        if (existing is null)
        {
            consent = PrivacyConsent.Create(guestId, request.PolicyVersion, choices, locale, _clock);
            _consents.Add(consent);
        }
        else
        {
            existing.Record(choices, locale, _clock);
            consent = existing;
        }

        return _consentService.ToStatus(guestId, new ConsentDecisionView(guestId, consent.PolicyVersion, choices.Analytics, choices.Preferences, choices.Marketing,
            consent.DecidedAtUtc, consent.Locale.ToString()));
    }
}
