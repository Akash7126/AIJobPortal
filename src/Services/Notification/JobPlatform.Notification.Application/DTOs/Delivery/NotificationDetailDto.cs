namespace JobPlatform.Notification.Application.DTOs.Delivery;

public sealed record NotificationDetailDto(Guid Id, string Channel, string Category, string Status, string DeliveryStatus, string? Subject, DateTime CreatedAtUtc, DateTime? SentAtUtc);
