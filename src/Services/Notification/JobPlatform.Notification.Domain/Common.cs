using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain;

public enum Channel
{
    Email,
    Sms,
    InApp
}

public enum InAppStatus
{
    Unread,
    Read,
    Deleted
}

public enum MessageStatus
{
    Pending,
    Sending,
    Sent,
    Failed,
    Suppressed,
    Digested
}

public enum DeliveryStatus
{
    Pending,
    Delivered,
    Failed
}

public enum DeliveryMode
{
    Immediate,
    Digest
}

/// <summary>Notification categories (handover 2). Mandatory ones cannot be disabled; essential ones are the only ones sent by SMS.</summary>
public static class Categories
{
    public const string SecurityAlert = "SecurityAlert";
    public const string Otp = "Otp";
    public const string PasswordReset = "PasswordReset";
    public const string Welcome = "Welcome";
    public const string SavedSearchMatch = "SavedSearchMatch";
    public const string WeeklyRecommendation = "WeeklyRecommendation";
    public const string JobConfirmation = "JobConfirmation";
    public const string News = "News";
    public const string System = "System";
    public const string Digest = "Digest";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        SecurityAlert, Otp, PasswordReset, Welcome, SavedSearchMatch, WeeklyRecommendation, JobConfirmation, News, System, Digest
    };

    /// <summary>The user cannot switch these off (3.6.1-03, 3.6.2-05 AC-02).</summary>
    public static readonly IReadOnlyCollection<string> Mandatory = new[] { SecurityAlert, Otp, PasswordReset };

    /// <summary>Marketing-like categories: e-mail must carry an unsubscribe link (3.6.1-05).</summary>
    public static readonly IReadOnlyCollection<string> Marketing = new[] { SavedSearchMatch, WeeklyRecommendation, News, Digest };

    public static readonly IReadOnlyCollection<string> DefaultEssentialSms = new[] { Otp, PasswordReset };

    public static bool IsMandatory(string category) => Mandatory.Contains(category, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string category) => All.Contains(category, StringComparer.OrdinalIgnoreCase);

    public static string Canonical(string category) => All.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)) ?? category;
}

/// <summary>Externally published error codes (handover 4 and 6).</summary>
public static class NotificationErrorCodes
{
    public const string InAppForbidden = "E-INAPPN-FORBIDDEN";
    public const string InAppNotFound = "E-INAPPN-NOT-FOUND";
    public const string EmailForbidden = "E-EMAILN-FORBIDDEN";
    public const string EmailUpstreamTimeout = "E-EMAILN-UPSTREAM-TIMEOUT";
    public const string SmsForbidden = "E-SMSN-FORBIDDEN";
    public const string SmsInvalidField = "E-SMSN-INVALID-FIELD";
    public const string SmsUpstreamTimeout = "E-SMSN-UPSTREAM-TIMEOUT";
    public const string InvalidField = "E-NOTIF-INVALID-FIELD";
    public const string MandatoryCategory = "E-NOTIF-MANDATORY-CATEGORY";
    public const string TemplateNotFound = "E-NOTIF-TEMPLATE-NOT-FOUND";
    public const string InvalidToken = "E-NOTIF-INVALID-TOKEN";
    public const string NotificationNotFound = "E-NOTIF-NOT-FOUND";
    public const string ContactUnavailable = "E-NOTIF-CONTACT-UNAVAILABLE";
    public const string TypeForbidden = "E-INAPPN-FORBIDDEN";
}

public static class NotificationRuleCodes
{
    public const string DuplicateMessage = "NT.Message.DUPLICATE";
    public const string MandatoryCannotDisable = "NT.Preference.MANDATORY_CANNOT_DISABLE";
    public const string PreferenceNotOwner = "NT.Preference.NOT_OWNER";
    public const string InvalidMobile = "NT.Sms.INVALID_MOBILE";
    public const string NotRecipient = "NT.InApp.NOT_RECIPIENT";
    public const string AlreadyDeleted = "NT.InApp.ALREADY_DELETED";
    public const string TemplateAdminOnly = "NT.Template.ADMIN_ONLY";
    public const string InvalidTemplate = "NT.Template.INVALID";
    public const string InvalidMessage = "NT.Message.INVALID";
    public const string InvalidTransition = "NT.Message.INVALID_TRANSITION";
    public const string ComplianceViolation = "NT.Compliance.VIOLATION";
    public const string InvalidType = "NT.Type.INVALID";
    public const string TypeAdminOnly = "NT.Type.ADMIN_ONLY";
    public const string PolicyAdminOnly = "NT.SmsPolicy.ADMIN_ONLY";
    public const string InvalidPolicy = "NT.SmsPolicy.INVALID";
    public const string InvalidConfirmation = "NT.Confirmation.INVALID";
}

/// <summary>The caller as the domain needs to know it.</summary>
public sealed record Actor(Guid Id, bool IsAdministrator);

internal static class Guard
{
    public static void Ensure(bool condition, string code, string message, string? externalCode = null, BusinessRuleKind kind = BusinessRuleKind.BusinessRule)
    {
        if (!condition)
        {
            throw new BusinessRuleViolationException(code, message, externalCode, kind);
        }
    }
}
