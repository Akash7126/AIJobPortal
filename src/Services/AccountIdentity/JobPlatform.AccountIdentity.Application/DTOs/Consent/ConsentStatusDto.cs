using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.AccountIdentity.Application.DTOs.Consent;

/// <summary>Everything a client needs to decide whether to show the banner and whether non-essential collection may start.</summary>
public sealed record ConsentStatusDto(
    Guid? GuestId,
    string CurrentPolicyVersion,
    string PrivacyPolicyUrl,
    LocalizedText BannerText,
    bool BannerRequired,
    ConsentChoicesDto Allowed,
    DateTime? DecidedAtUtc);
