namespace JobPlatform.Notification.Application.DTOs.Admin;

public sealed record NotificationTypeDto(string Code, string Icon, string Colour, string TextAlternative, bool IsMandatory);
