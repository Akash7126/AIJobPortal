using System.Text.RegularExpressions;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain;

/// <summary>
/// Versioned e-mail template with {{placeholders}} that each carry a default (US-3.6.1-02). Editing is administrator-only and always creates a new version;
/// rendering never leaves a broken {{token}}: a missing value falls back to the placeholder's default.
/// </summary>
public sealed partial class EmailTemplate : Entity<Guid>
{
    private EmailTemplate()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string Locale { get; private set; } = "en";
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>Placeholder name to default value.</summary>
    public IReadOnlyDictionary<string, string> Placeholders { get; private set; } = new Dictionary<string, string>();

    public Guid? EditedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static EmailTemplate Create(string code, string locale, string subject, string body, IReadOnlyDictionary<string, string> placeholders, DateTime nowUtc) =>
        Build(code, 1, locale, subject, body, placeholders, null, nowUtc);

    /// <summary>Administrator edit: a new version; the old one stays for batches that started with it (AC-03).</summary>
    public EmailTemplate NewVersion(Actor actor, string subject, string body, IReadOnlyDictionary<string, string> placeholders, DateTime nowUtc)
    {
        Guard.Ensure(actor.IsAdministrator, NotificationRuleCodes.TemplateAdminOnly, "Only administrators may edit templates.", NotificationErrorCodes.EmailForbidden,
            BusinessRuleKind.Forbidden);
        return Build(Code, Version + 1, Locale, subject, body, placeholders, actor.Id, nowUtc);
    }

    private static EmailTemplate Build(string code, int version, string locale, string subject, string body, IReadOnlyDictionary<string, string> placeholders, Guid? editor,
        DateTime nowUtc)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(subject) && !string.IsNullOrWhiteSpace(body), NotificationRuleCodes.InvalidTemplate,
            "A template needs a code, a subject and a body.", NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        var used = Used(subject).Concat(Used(body)).Distinct(StringComparer.Ordinal).ToList();
        var undeclared = used.Where(name => !placeholders.ContainsKey(name)).ToList();
        Guard.Ensure(undeclared.Count == 0, NotificationRuleCodes.InvalidTemplate, $"Placeholders without a default: {string.Join(", ", undeclared)}.",
            NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        return new EmailTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Version = version,
            Locale = locale,
            Subject = subject,
            Body = body,
            Placeholders = new Dictionary<string, string>(placeholders),
            EditedBy = editor,
            CreatedAtUtc = nowUtc
        };
    }

    public (string Subject, string Body) Render(IReadOnlyDictionary<string, string> values) => (Fill(Subject, values), Fill(Body, values));

    private string Fill(string text, IReadOnlyDictionary<string, string> values) =>
        PlaceholderPattern().Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            if (values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return Placeholders.TryGetValue(name, out var fallback) ? fallback : string.Empty;
        });

    private static IEnumerable<string> Used(string text) => PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value);

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}

/// <summary>Administrator-defined kind of in-app notification with icon, colour and a text alternative (US-3.6.2-06, THR-066/067).</summary>
public sealed class NotificationType : Entity<string>
{
    public const string GenericCode = "generic";

    public static readonly IReadOnlyCollection<string> AllowedIcons = new[] { "bell", "briefcase", "search", "star", "shield", "mail", "info", "warning", "check", "user" };

    private NotificationType()
    {
    }

    public string Icon { get; private set; } = "bell";
    public string Colour { get; private set; } = "#1D4ED8";
    public string TextAlternative { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; }

    /// <summary>The indicator used for an unrecognised type: never a broken icon (INV-05).</summary>
    public static NotificationType Generic { get; } = new() { Id = GenericCode, Icon = "bell", Colour = "#1D4ED8", TextAlternative = "Notification" };

    public static NotificationType Define(Actor actor, string code, string icon, string colour, string textAlternative, bool mandatory)
    {
        Guard.Ensure(actor.IsAdministrator, NotificationRuleCodes.TypeAdminOnly, "Only administrators may define notification types.", NotificationErrorCodes.InAppForbidden,
            BusinessRuleKind.Forbidden);
        Validate(code, icon, colour, textAlternative);
        return new NotificationType { Id = code, Icon = icon, Colour = colour, TextAlternative = textAlternative, IsMandatory = mandatory };
    }

    public void Redefine(Actor actor, string icon, string colour, string textAlternative, bool mandatory)
    {
        Guard.Ensure(actor.IsAdministrator, NotificationRuleCodes.TypeAdminOnly, "Only administrators may define notification types.", NotificationErrorCodes.InAppForbidden,
            BusinessRuleKind.Forbidden);
        Validate(Id, icon, colour, textAlternative);
        Icon = icon;
        Colour = colour;
        TextAlternative = textAlternative;
        IsMandatory = mandatory;
    }

    private static void Validate(string code, string icon, string colour, string textAlternative)
    {
        Guard.Ensure(!string.IsNullOrWhiteSpace(code) && code.Length <= 64, NotificationRuleCodes.InvalidType, "A type code is required.", NotificationErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(AllowedIcons.Contains(icon), NotificationRuleCodes.InvalidType, "The icon is not in the allow-list.", NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(!string.IsNullOrWhiteSpace(textAlternative), NotificationRuleCodes.InvalidType, "A text alternative is required.", NotificationErrorCodes.InvalidField,
            BusinessRuleKind.InvalidInput);
        Guard.Ensure(Contrast.OnWhite(colour) >= 4.5, NotificationRuleCodes.InvalidType, "The colour needs a contrast ratio of at least 4.5:1 (WCAG AA).",
            NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
    }
}

/// <summary>WCAG contrast of a #RRGGBB colour against white.</summary>
public static class Contrast
{
    public static double OnWhite(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return 0;
        }

        double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var luminance = 0.2126 * Channel((rgb >> 16) & 0xFF) + 0.7152 * Channel((rgb >> 8) & 0xFF) + 0.0722 * Channel(rgb & 0xFF);
        return 1.05 / (luminance + 0.05);
    }
}

/// <summary>Versioned list of essential SMS categories (US-3.6.3-03). A running batch keeps the version in effect when it started.</summary>
public sealed class SmsPolicy : Entity<int>
{
    private SmsPolicy()
    {
    }

    public IReadOnlyCollection<string> EssentialCategories { get; private set; } = Categories.DefaultEssentialSms;
    public Guid? ChangedBy { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    public static SmsPolicy Initial(DateTime nowUtc) => new() { Id = 1, ChangedAtUtc = nowUtc };

    public bool IsEssential(string category) => EssentialCategories.Contains(Categories.Canonical(category), StringComparer.OrdinalIgnoreCase);

    public SmsPolicy NewVersion(Actor actor, IEnumerable<string> categories, DateTime nowUtc)
    {
        Guard.Ensure(actor.IsAdministrator, NotificationRuleCodes.PolicyAdminOnly, "Only administrators may change the essential SMS categories.",
            NotificationErrorCodes.SmsForbidden, BusinessRuleKind.Forbidden);
        var list = categories.Select(Categories.Canonical).Distinct().ToList();
        Guard.Ensure(list.All(Categories.IsKnown), NotificationRuleCodes.InvalidPolicy, "Unknown category.", NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(Categories.DefaultEssentialSms.All(list.Contains), NotificationRuleCodes.InvalidPolicy, "One-time codes and password resets must stay essential.",
            NotificationErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        return new SmsPolicy { Id = Id + 1, EssentialCategories = list, ChangedBy = actor.Id, ChangedAtUtc = nowUtc };
    }
}
