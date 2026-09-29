namespace JobPlatform.Notification.Application.DTOs.Delivery;

public sealed record TransactionalSmsResult(bool Accepted, string Status);
