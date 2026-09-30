using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Admin;

public sealed record ConfigureEssentialSmsCategoriesCommand(IReadOnlyCollection<string> Categories)
    : AdminRequest(NotificationErrorCodes.SmsForbidden), ICommand<IReadOnlyCollection<string>>;
