using JobPlatform.Notification.Application.Commands.Admin;
using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.Services.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class DefineNotificationTypeHandler : ICommandHandler<DefineNotificationTypeCommand, NotificationTypeDto>
{
    private readonly INotificationTypeRepository _types;
    private readonly AdminService _adminService;

    public DefineNotificationTypeHandler(INotificationTypeRepository types, AdminService adminService)
    {
        _types = types;
        _adminService = adminService;
    }

    public async Task<Result<NotificationTypeDto>> Handle(DefineNotificationTypeCommand request, CancellationToken ct)
    {
        var existing = await _types.GetAsync(request.Code, ct);
        if (existing is null)
        {
            existing = NotificationType.Define(_adminService.Me, request.Code, request.Icon, request.Colour, request.TextAlternative, request.IsMandatory);
            _types.Add(existing);
        }
        else
        {
            existing.Redefine(_adminService.Me, request.Icon, request.Colour, request.TextAlternative, request.IsMandatory);
        }

        return AdminService.ToDto(existing);
    }
}
