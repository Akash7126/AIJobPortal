using FluentValidation;
using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Ports;

namespace JobPlatform.Notification.Application.Services.Preferences;

/// <summary>Logic shared by the preference request handlers.</summary>
internal sealed class PreferenceService
{
    private readonly INotificationPreferenceRepository _repository;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PreferenceService(INotificationPreferenceRepository repository, ICurrentUser user, TimeProvider clock)
    {
        _repository = repository;
        _user = user;
        _clock = clock;
    }

    public DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public Actor Me => new(_user.UserId!.Value, false);

    /// <summary>Loads or starts the user's preferences. Saves are last-write-wins (no optimistic-concurrency token, AC-04).</summary>
    public async Task<NotificationPreference> Load(Guid accountId, CancellationToken ct)
    {
        var preference = await _repository.GetAsync(accountId, ct);
        if (preference is null)
        {
            preference = NotificationPreference.Default(accountId, Now);
            _repository.Add(preference);
        }

        return preference;
    }

    public static NotificationPreferencesDto ToDto(NotificationPreference p, string channel)
    {
        var known = Categories.All.Where(c => c != Categories.Digest).ToList();
        return channel switch
        {
            "sms" => new NotificationPreferencesDto(new Dictionary<string, bool>(), Categories.Mandatory, null, p.SmsOptedIn, p.Mobile is null ? null : Masking.Mobile(p.Mobile),
                p.Unsubscribed),
            "in-app" => new NotificationPreferencesDto(known.ToDictionary(c => c, p.InAppAllowed), Categories.Mandatory, null, null, null, p.Unsubscribed),
            _ => new NotificationPreferencesDto(known.ToDictionary(c => c, p.EmailAllowed), Categories.Mandatory, p.EmailMode.ToString(), null, null, p.Unsubscribed)
        };
    }
}
