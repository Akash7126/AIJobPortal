using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.ApiCredentials;

public sealed class IssueApiCredentialValidator : AbstractValidator<IssueApiCredentialCommand>
{
    public IssueApiCredentialValidator(TimeProvider clock)
    {
        When(x => x.IpWhitelist is { Count: > 0 }, () =>
            RuleForEach(x => x.IpWhitelist!).Must(BeIpOrCidr).WithErrorCode("VAL.IpWhitelist.Invalid"));
        When(x => x.MaxRequests.HasValue, () =>
            RuleFor(x => x.MaxRequests!.Value).GreaterThan(0).OverridePropertyName("MaxRequests").WithErrorCode("VAL.MaxRequests.Invalid"));
        When(x => x.PeriodSeconds.HasValue, () =>
            RuleFor(x => x.PeriodSeconds!.Value).GreaterThan(0).OverridePropertyName("PeriodSeconds").WithErrorCode("VAL.PeriodSeconds.Invalid"));
        When(x => x.ExpiresAtUtc.HasValue, () =>
            RuleFor(x => x.ExpiresAtUtc!.Value).Must(e =>
                    e > clock.GetUtcNow().UtcDateTime && e <= clock.GetUtcNow().UtcDateTime.AddDays(AccountDefaults.MaxCredentialLifetimeDays))
                .OverridePropertyName("ExpiresAtUtc").WithErrorCode("VAL.ExpiresAtUtc.OutOfRange"));
    }

    private static bool BeIpOrCidr(string? value) =>
        !string.IsNullOrWhiteSpace(value) && (value.Contains('/') ? System.Net.IPNetwork.TryParse(value, out _) : System.Net.IPAddress.TryParse(value, out _));
}
