using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Application.Queries.Preferences;
using JobPlatform.Notification.Application.Services.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Preferences;

internal sealed class GetNotificationPreferencesHandler : IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly INotificationPreferenceRepository _repository;
    private readonly PreferenceService _preferenceService;

    public GetNotificationPreferencesHandler(INotificationPreferenceRepository repository, PreferenceService preferenceService)
    {
        _repository = repository;
        _preferenceService = preferenceService;
    }

    public async Task<Result<NotificationPreferencesDto>> Handle(GetNotificationPreferencesQuery request, CancellationToken ct) =>
        PreferenceService.ToDto(await _repository.GetAsync(_preferenceService.Me.Id, ct) ?? NotificationPreference.Default(_preferenceService.Me.Id, _preferenceService.Now), request.Channel);
}
