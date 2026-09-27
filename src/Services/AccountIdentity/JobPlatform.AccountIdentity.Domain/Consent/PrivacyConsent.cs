using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Consent;

public static class ConsentRuleCodes
{
    public const string PolicyVersionRequired = "AI.PrivacyConsent.POLICY_VERSION_REQUIRED";
}

/// <summary>Cookie/consent categories. "Necessary" is always granted; non-essential collection is withheld until a choice exists (US-4.1-04 AC-04).</summary>
public sealed class ConsentChoices : ValueObject
{
    public ConsentChoices(bool analytics, bool preferences, bool marketing)
    {
        Analytics = analytics;
        Preferences = preferences;
        Marketing = marketing;
    }

    public bool Necessary => true;
    public bool Analytics { get; }
    public bool Preferences { get; }
    public bool Marketing { get; }

    public static ConsentChoices NecessaryOnly { get; } = new(false, false, false);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Analytics;
        yield return Preferences;
        yield return Marketing;
    }
}

/// <summary>A guest's consent decision for one policy version. Recording again for the same guest+version updates rather than duplicates (US-4.1-04 AC-05).</summary>
public sealed class PrivacyConsent : AggregateRoot<Guid>
{
    private PrivacyConsent()
    {
    }

    public Guid GuestId { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public ConsentChoices Choices { get; private set; } = ConsentChoices.NecessaryOnly;
    public DateTime DecidedAtUtc { get; private set; }
    public Language Locale { get; private set; }

    public static PrivacyConsent Create(Guid guestId, string policyVersion, ConsentChoices choices, Language locale, TimeProvider clock)
    {
        EnsureVersion(policyVersion);
        return new PrivacyConsent
        {
            Id = Guid.NewGuid(),
            GuestId = guestId,
            PolicyVersion = policyVersion,
            Choices = choices,
            Locale = locale,
            DecidedAtUtc = clock.GetUtcNow().UtcDateTime
        };
    }

    /// <summary>Replaces the choices of an existing decision (the guest changed their mind). Returns false when nothing changed.</summary>
    public bool Record(ConsentChoices choices, Language locale, TimeProvider clock)
    {
        if (Choices.Equals(choices) && Locale == locale)
        {
            return false;
        }

        Choices = choices;
        Locale = locale;
        DecidedAtUtc = clock.GetUtcNow().UtcDateTime;
        return true;
    }

    private static void EnsureVersion(string policyVersion) =>
        Guard.Ensure(!string.IsNullOrWhiteSpace(policyVersion), ConsentRuleCodes.PolicyVersionRequired, "A policy version is required.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
}
