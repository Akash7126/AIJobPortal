using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.DTOs.Delivery;

public sealed record SendSpec(Guid MessageId, Channel Channel, Guid Recipient, string Category, string Subject, string Body);
