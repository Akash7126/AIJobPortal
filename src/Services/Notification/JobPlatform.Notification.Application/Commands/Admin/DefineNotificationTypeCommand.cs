using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Admin;

public sealed record DefineNotificationTypeCommand(string Code, string Icon, string Colour, string TextAlternative, bool IsMandatory)
    : AdminRequest(NotificationErrorCodes.TypeForbidden), ICommand<NotificationTypeDto>;
