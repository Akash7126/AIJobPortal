using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Services.InApp;

/// <summary>Logic shared by the in app request handlers.</summary>
internal sealed class InAppService
{
    private readonly IInAppNotificationRepository _repository;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public InAppService(IInAppNotificationRepository repository, ICurrentUser user, TimeProvider clock)
    {
        _repository = repository;
        _user = user;
        _clock = clock;
    }

    public static readonly Error NotFound = Error.NotFound(NotificationErrorCodes.InAppNotFound, "The notification was not found.");

    public Actor Me => new(_user.UserId!.Value, _user.ActorType == SharedKernel.Common.Enums.ActorType.Administrator);

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>A deleted notification is gone (INV-03 to E-INAPPN-NOT-FOUND); someone else's notification is reported by the aggregate as forbidden.</summary>
    public async Task<InAppNotification?> Load(Guid id, CancellationToken ct)
    {
        var notification = await _repository.GetAsync(id, ct);
        return notification is { Status: InAppStatus.Deleted } ? null : notification;
    }
}
