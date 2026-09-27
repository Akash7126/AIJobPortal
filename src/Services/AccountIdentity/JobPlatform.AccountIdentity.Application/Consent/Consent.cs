using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Application.Consent;

// ---------------------------------------------------------------------- record consent (US-4.1-04)

/// <param name="GuestId">Anonymous cookie id; a new one is minted when the guest has none yet.</param>
public sealed record RecordPrivacyConsentCommand(Guid? GuestId, string PolicyVersion, bool Analytics, bool Preferences, bool Marketing, string? Locale,
    string? IdempotencyKey) : ICommand<ConsentStatusDto>, IIdempotentCommand;

public sealed record ConsentChoicesDto(bool Necessary, bool Analytics, bool Preferences, bool Marketing);

/// <summary>Everything a client needs to decide whether to show the banner and whether non-essential collection may start.</summary>
public sealed record ConsentStatusDto(
    Guid? GuestId,
    string CurrentPolicyVersion,
    string PrivacyPolicyUrl,
    LocalizedText BannerText,
    bool BannerRequired,
    ConsentChoicesDto Allowed,
    DateTime? DecidedAtUtc);

public sealed record GetCurrentConsentQuery(Guid? GuestId) : IQuery<ConsentStatusDto>;

public sealed class RecordPrivacyConsentValidator : AbstractValidator<RecordPrivacyConsentCommand>
{
    public RecordPrivacyConsentValidator(IOptions<ConsentOptions> options)
    {
        RuleFor(x => x.PolicyVersion).NotEmpty().WithErrorCode("VAL.PolicyVersion.Required").DependentRules(() =>
            RuleFor(x => x.PolicyVersion).Equal(options.Value.CurrentPolicyVersion).WithErrorCode("VAL.PolicyVersion.NotCurrent"));
        When(x => !string.IsNullOrEmpty(x.Locale), () =>
            RuleFor(x => x.Locale!).Must(l => l is "ar" or "en").WithErrorCode("VAL.Locale.Invalid"));
        When(x => x.GuestId.HasValue, () =>
            RuleFor(x => x.GuestId!.Value).NotEqual(Guid.Empty).OverridePropertyName("GuestId").WithErrorCode("VAL.GuestId.Invalid"));
    }
}

internal sealed class ConsentHandlers :
    ICommandHandler<RecordPrivacyConsentCommand, ConsentStatusDto>,
    IQueryHandler<GetCurrentConsentQuery, ConsentStatusDto>
{
    private readonly IPrivacyConsentRepository _consents;
    private readonly IIdentityReadStore _store;
    private readonly ConsentOptions _options;
    private readonly TimeProvider _clock;

    public ConsentHandlers(IPrivacyConsentRepository consents, IIdentityReadStore store, IOptions<ConsentOptions> options, TimeProvider clock)
    {
        _consents = consents;
        _store = store;
        _options = options.Value;
        _clock = clock;
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

        return ToStatus(guestId, new ConsentDecisionView(guestId, consent.PolicyVersion, choices.Analytics, choices.Preferences, choices.Marketing,
            consent.DecidedAtUtc, consent.Locale.ToString()));
    }

    public async Task<Result<ConsentStatusDto>> Handle(GetCurrentConsentQuery request, CancellationToken ct)
    {
        if (request.GuestId is not { } guestId)
        {
            return ToStatus(null, null);
        }

        return ToStatus(guestId, await _store.GetConsentAsync(guestId, _options.CurrentPolicyVersion, ct));
    }

    private ConsentStatusDto ToStatus(Guid? guestId, ConsentDecisionView? decision) => new(
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
