using JobPlatform.Notification.Application.Commands.InApp;
using JobPlatform.Notification.Application.Services.InApp;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.InApp;

internal sealed class MarkNotificationReadHandler : ICommandHandler<MarkNotificationReadCommand, Unit>
{
    private readonly InAppService _inAppService;

    public MarkNotificationReadHandler(InAppService inAppService) => _inAppService = inAppService;

    public async Task<Result<Unit>> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await _inAppService.Load(request.Id, ct);
        if (notification is null)
        {
            return InAppService.NotFound;
        }

        notification.MarkRead(_inAppService.Me, _inAppService.Now);
        return Result.Success();
    }
}
