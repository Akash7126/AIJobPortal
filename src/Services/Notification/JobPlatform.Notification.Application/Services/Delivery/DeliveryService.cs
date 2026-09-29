using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Services.Delivery;

/// <summary>Logic shared by the delivery request handlers.</summary>
internal sealed class DeliveryService
{
    private readonly TimeProvider _clock;

    public DeliveryService(TimeProvider clock) => _clock = clock;

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public static Error NotFound => Error.NotFound(NotificationErrorCodes.NotificationNotFound, "The notification was not found.");
}
