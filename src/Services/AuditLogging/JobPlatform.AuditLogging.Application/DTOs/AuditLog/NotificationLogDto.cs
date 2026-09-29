namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record NotificationLogDto(Guid NotificationId, string Channel, string Category, string MaskedRecipient, string? Subject, string Status, DateTime SentAtUtc);
