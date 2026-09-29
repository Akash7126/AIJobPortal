namespace JobPlatform.Notification.Application.DTOs.Preferences;

public sealed record NotificationPreferencesDto(IReadOnlyDictionary<string, bool> Categories, IReadOnlyCollection<string> Mandatory, string? Mode, bool? SmsOptedIn,
    string? MobileMasked, IReadOnlyCollection<string> Unsubscribed);
