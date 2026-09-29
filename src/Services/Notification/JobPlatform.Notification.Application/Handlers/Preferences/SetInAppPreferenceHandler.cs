using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Application.Services.Preferences;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Preferences;

internal sealed class SetInAppPreferenceHandler : ICommandHandler<SetInAppPreferenceCommand, NotificationPreferencesDto>
{
    private readonly PreferenceService _preferenceService;

    public SetInAppPreferenceHandler(PreferenceService preferenceService) => _preferenceService = preferenceService;

    public async Task<Result<NotificationPreferencesDto>> Handle(SetInAppPreferenceCommand request, CancellationToken ct)
    {
        var preference = await _preferenceService.Load(_preferenceService.Me.Id, ct);
        preference.SetInAppPreference(_preferenceService.Me, request.Categories, _preferenceService.Now);
        return PreferenceService.ToDto(preference, "in-app");
    }
}
