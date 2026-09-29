namespace JobPlatform.Notification.Application.DTOs.Admin;

public sealed record SmsDeliveryDto(Guid Id, string MaskedRecipient, string Category, string Status, string DeliveryStatus, string? ErrorCode, DateTime CreatedAtUtc);
