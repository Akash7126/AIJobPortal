using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain;

/// <summary>
/// Per-user channel and category settings (proposed aggregate). Owner-only (INV-14) with the channel's own forbidden code, mandatory categories cannot be
/// switched off (INV-12), the mobile number must be valid (INV-13). Plain entity on purpose: concurrent edits are last-write-wins (AC-04), there is no
/// optimistic-concurrency token.
/// </summary>
public sealed class NotificationPreference : Entity<Guid>
{
    private readonly Dictionary<string, bool> _email = new();
    private readonly Dictionary<string, bool> _inApp = new();
    private readonly HashSet<string> _unsubscribed = new();

    private NotificationPreference()
    {
    }

    public IReadOnlyDictionary<string, bool> EmailCategories => _email;
    public IReadOnlyDictionary<string, bool> InAppCategories => _inApp;
    public IReadOnlyCollection<string> Unsubscribed => _unsubscribed;
    public DeliveryMode EmailMode { get; private set; }
    public bool SmsOptedIn { get; private set; }
    public string? Mobile { get; private set; }
    public bool Suspended { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Account id is the key; a user without saved preferences gets the defaults (everything on, immediate).</summary>
    public static NotificationPreference Default(Guid accountId, DateTime nowUtc) => new() { Id = accountId, UpdatedAtUtc = nowUtc };

    public bool EmailAllowed(string category) => Categories.IsMandatory(category) || !_unsubscribed.Contains(Categories.Canonical(category)) && _email.GetValueOrDefault(Categories.Canonical(category), true);

    public bool InAppAllowed(string category) => Categories.IsMandatory(category) || _inApp.GetValueOrDefault(Categories.Canonical(category), true);

    public bool IsUnsubscribed(string category) => _unsubscribed.Contains(Categories.Canonical(category));

    public void SetEmailPreference(Actor actor, IReadOnlyDictionary<string, bool> categories, DeliveryMode mode, DateTime nowUtc)
    {
        EnsureOwner(actor, NotificationErrorCodes.EmailForbidden);
        Apply(_email, categories);
        EmailMode = mode;
        // Re-enabling a category clears an earlier unsubscribe of it.
        foreach (var enabled in categories.Where(c => c.Value).Select(c => Categories.Canonical(c.Key)))
        {
            _unsubscribed.Remove(enabled);
        }

        UpdatedAtUtc = nowUtc;
    }

    public void SetInAppPreference(Actor actor, IReadOnlyDictionary<string, bool> categories, DateTime nowUtc)
    {
        EnsureOwner(actor, NotificationErrorCodes.InAppForbidden);
        Apply(_inApp, categories);
        UpdatedAtUtc = nowUtc;
    }

    public void SetSmsOptIn(Actor actor, string? mobile, bool optIn, DateTime nowUtc)
    {
        EnsureOwner(actor, NotificationErrorCodes.SmsForbidden);
        if (optIn)
        {
            Guard.Ensure(MobileNumber.TryCreate(mobile, out var number), NotificationRuleCodes.InvalidMobile, "A valid mobile number is required to opt in to SMS.",
                NotificationErrorCodes.SmsInvalidField, BusinessRuleKind.InvalidInput);
            Mobile = number!.Value;
        }

        SmsOptedIn = optIn;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>One-click unsubscribe from a category (honoured for every later send). Mandatory categories cannot be unsubscribed.</summary>
    public void Unsubscribe(string category, DateTime nowUtc)
    {
        var canonical = Categories.Canonical(category);
        Guard.Ensure(Categories.IsKnown(canonical), NotificationRuleCodes.MandatoryCannotDisable, "Unknown category.", NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(!Categories.IsMandatory(canonical), NotificationRuleCodes.MandatoryCannotDisable, "A mandatory notification cannot be unsubscribed.",
            NotificationErrorCodes.MandatoryCategory, BusinessRuleKind.BusinessRule);
        _unsubscribed.Add(canonical);
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>The account was deactivated: no further notifications (AccountSuspended reaction).</summary>
    public void Suspend(DateTime nowUtc)
    {
        Suspended = true;
        UpdatedAtUtc = nowUtc;
    }

    private static void Apply(Dictionary<string, bool> target, IReadOnlyDictionary<string, bool> categories)
    {
        foreach (var (category, enabled) in categories)
        {
            var canonical = Categories.Canonical(category);
            Guard.Ensure(Categories.IsKnown(canonical), NotificationRuleCodes.MandatoryCannotDisable, $"Unknown category '{category}'.", NotificationErrorCodes.InvalidField,
                BusinessRuleKind.InvalidInput);
            Guard.Ensure(enabled || !Categories.IsMandatory(canonical), NotificationRuleCodes.MandatoryCannotDisable, $"'{canonical}' is mandatory and cannot be disabled.",
                NotificationErrorCodes.MandatoryCategory, BusinessRuleKind.BusinessRule);
            target[canonical] = enabled;
        }
    }

    private void EnsureOwner(Actor actor, string forbiddenCode) =>
        Guard.Ensure(actor.Id == Id, NotificationRuleCodes.PreferenceNotOwner, "Only the owner may change these preferences.", forbiddenCode, BusinessRuleKind.Forbidden);
}
