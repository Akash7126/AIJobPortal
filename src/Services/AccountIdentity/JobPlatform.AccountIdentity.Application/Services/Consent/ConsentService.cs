using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Consent;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Application.Services.Consent;

/// <summary>Logic shared by the consent request handlers.</summary>
internal sealed class ConsentService
{
    private readonly ConsentOptions _options;

    public ConsentService(IOptions<ConsentOptions> options) => _options = options.Value;

    public ConsentStatusDto ToStatus(Guid? guestId, ConsentDecisionView? decision) => new(
        guestId,
        _options.CurrentPolicyVersion,
        _options.PrivacyPolicyUrl,
        new LocalizedText(_options.BannerTextAr, _options.BannerTextEn),
        BannerRequired: decision is null,
        // Until a choice exists only essential collection is allowed (AC-04).
        decision is null
            ? new ConsentChoicesDto(true, false, false, false)
            : new ConsentChoicesDto(true, decision.Analytics, decision.Preferences, decision.Marketing),
        decision?.DecidedAtUtc);
}
