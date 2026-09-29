using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.Consent;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Application.Validators.Consent;

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
