using JobPlatform.Notification.Application.Commands.InApp;
using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Application.Services.InApp;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.InApp;

internal sealed class TakeNotificationActionHandler : ICommandHandler<TakeNotificationActionCommand, NotificationActionDto>
{
    private readonly InAppService _inAppService;

    public TakeNotificationActionHandler(InAppService inAppService) => _inAppService = inAppService;

    public async Task<Result<NotificationActionDto>> Handle(TakeNotificationActionCommand request, CancellationToken ct)
    {
        var notification = await _inAppService.Load(request.Id, ct);
        return notification is null ? InAppService.NotFound : new NotificationActionDto(notification.TakeAction(_inAppService.Me, _inAppService.Now));
    }
}
