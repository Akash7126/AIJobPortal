using JobPlatform.Notification.Application.Commands.InApp;
using JobPlatform.Notification.Application.Services.InApp;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.InApp;

internal sealed class DeleteNotificationHandler : ICommandHandler<DeleteNotificationCommand, Unit>
{
    private readonly InAppService _inAppService;

    public DeleteNotificationHandler(InAppService inAppService) => _inAppService = inAppService;

    public async Task<Result<Unit>> Handle(DeleteNotificationCommand request, CancellationToken ct)
    {
        var notification = await _inAppService.Load(request.Id, ct);
        if (notification is null)
        {
            return InAppService.NotFound;
        }

        notification.Delete(_inAppService.Me, _inAppService.Now);
        return Result.Success();
    }
}
