using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Application.Services.Preferences;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Preferences;

internal sealed class SetSmsOptInHandler : ICommandHandler<SetSmsOptInCommand, NotificationPreferencesDto>
{
    private readonly PreferenceService _preferenceService;

    public SetSmsOptInHandler(PreferenceService preferenceService) => _preferenceService = preferenceService;

    public async Task<Result<NotificationPreferencesDto>> Handle(SetSmsOptInCommand request, CancellationToken ct)
    {
        var preference = await _preferenceService.Load(_preferenceService.Me.Id, ct);
        preference.SetSmsOptIn(_preferenceService.Me, request.Mobile, request.OptIn, _preferenceService.Now);
        return PreferenceService.ToDto(preference, "sms");
    }
}
