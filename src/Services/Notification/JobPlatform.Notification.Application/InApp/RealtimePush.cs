using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Application.Interfaces;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.InApp;

/// <summary>
/// After the notification is committed, push it to the recipient's live connections (US-3.6.2-02, target 3 s). Failures are logged by the dispatcher and never
/// undo the commit: an offline or unreachable user still finds the notification Unread in the center at next login (AC-02).
/// </summary>
internal sealed class RealtimePushHandler : IDomainEventHandler<InAppNotificationCreatedDomainEvent>
{
    private readonly IInAppNotificationRepository _repository;
    private readonly INotificationTypeRepository _types;
    private readonly IRealtimeNotifier _notifier;

    public RealtimePushHandler(IInAppNotificationRepository repository, INotificationTypeRepository types, IRealtimeNotifier notifier)
    {
        _repository = repository;
        _types = types;
        _notifier = notifier;
    }

    public async Task Handle(InAppNotificationCreatedDomainEvent domainEvent, CancellationToken ct)
    {
        var notification = await _repository.GetAsync(domainEvent.NotificationId, ct);
        if (notification is null)
        {
            return;
        }

        var type = await _types.GetAsync(notification.TypeCode, ct) ?? NotificationType.Generic;
        await _notifier.PushAsync(notification.RecipientAccountId, new InAppNotificationDto(notification.Id, notification.TypeCode, notification.TitleEn, notification.BodyEn,
            notification.ActionUrl, notification.Status.ToString(), notification.CreatedAtUtc, notification.ReadAtUtc, type.Icon, type.Colour, type.TextAlternative), ct);
    }
}
