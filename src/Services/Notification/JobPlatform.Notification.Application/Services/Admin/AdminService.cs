using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Ports;

namespace JobPlatform.Notification.Application.Services.Admin;

/// <summary>Logic shared by the admin request handlers.</summary>
internal sealed class AdminService
{
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AdminService(ICurrentUser user, TimeProvider clock)
    {
        _user = user;
        _clock = clock;
    }

    public Actor Me => new(_user.UserId!.Value, _user.ActorType == SharedKernel.Common.Enums.ActorType.Administrator);

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public static EmailTemplateDto ToDto(EmailTemplate t) => new(t.Code, t.Locale, t.Version, t.Subject, t.Body, t.Placeholders);

    public static NotificationTypeDto ToDto(NotificationType t) => new(t.Id, t.Icon, t.Colour, t.TextAlternative, t.IsMandatory);
}
