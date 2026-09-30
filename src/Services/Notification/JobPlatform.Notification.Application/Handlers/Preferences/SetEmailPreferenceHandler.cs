using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Application.Services.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Preferences;

internal sealed class SetEmailPreferenceHandler : ICommandHandler<SetEmailPreferenceCommand, NotificationPreferencesDto>
{
    private readonly PreferenceService _preferenceService;

    public SetEmailPreferenceHandler(PreferenceService preferenceService) => _preferenceService = preferenceService;

    public async Task<Result<NotificationPreferencesDto>> Handle(SetEmailPreferenceCommand request, CancellationToken ct)
    {
        var preference = await _preferenceService.Load(_preferenceService.Me.Id, ct);
        preference.SetEmailPreference(_preferenceService.Me, request.Categories, Enum.Parse<DeliveryMode>(request.Mode, true), _preferenceService.Now);
        return PreferenceService.ToDto(preference, "email");
    }
}
