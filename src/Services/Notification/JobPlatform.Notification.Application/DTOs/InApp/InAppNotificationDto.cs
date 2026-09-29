namespace JobPlatform.Notification.Application.DTOs.InApp;

public sealed record InAppNotificationDto(Guid Id, string TypeCode, string Title, string Body, string? ActionUrl, string Status, DateTime CreatedAtUtc, DateTime? ReadAtUtc,
    string Icon, string Colour, string IconTextAlternative);
