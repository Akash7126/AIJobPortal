using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Ports;

namespace JobPlatform.AuditLogging.Application.Services.AuditLog;

/// <summary>Logic shared by the notification log query request handlers.</summary>
internal sealed class NotificationLogQueryService
{
    private readonly ICurrentUser _user;

    public NotificationLogQueryService(ICurrentUser user) => _user = user;

    public Viewer Viewer() => new(_user.ActorType, _user.UserId);
}
