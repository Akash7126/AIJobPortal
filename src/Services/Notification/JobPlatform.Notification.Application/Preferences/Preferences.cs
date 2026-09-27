using FluentValidation;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.Notification.Application.Preferences;

// ---------------------------------------------------------------------- preferences (US-3.6.1-03, 3.6.2-05, 3.6.3-02) - always the caller's own

public sealed record SetEmailPreferenceCommand(IReadOnlyDictionary<string, bool> Categories, string Mode)
    : AuthenticatedRequest(NotificationErrorCodes.EmailForbidden), ICommand<NotificationPreferencesDto>;

public sealed record SetInAppPreferenceCommand(IReadOnlyDictionary<string, bool> Categories)
    : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand<NotificationPreferencesDto>;

public sealed record SetSmsOptInCommand(string? Mobile, bool OptIn) : AuthenticatedRequest(NotificationErrorCodes.SmsForbidden), ICommand<NotificationPreferencesDto>;

/// <summary>channel is email, in-app or sms.</summary>
public sealed record GetNotificationPreferencesQuery(string Channel) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), IQuery<NotificationPreferencesDto>;

/// <summary>Anonymous one-click unsubscribe from the signed token in an e-mail (3.6.1-05 AC-02).</summary>
public sealed record UnsubscribeCommand(string Token) : ICommand<UnsubscribeResult>;

public sealed record UnsubscribeResult(string Category);

public sealed class SetEmailPreferenceValidator : AbstractValidator<SetEmailPreferenceCommand>
{
    public SetEmailPreferenceValidator()
    {
        RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
        RuleFor(x => x.Mode).Must(m => Enum.TryParse<DeliveryMode>(m, true, out _)).WithErrorCode("VAL.Mode.Invalid");
    }
}

public sealed class SetInAppPreferenceValidator : AbstractValidator<SetInAppPreferenceCommand>
{
    public SetInAppPreferenceValidator() => RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
}

public sealed class SetSmsOptInValidator : AbstractValidator<SetSmsOptInCommand>
{
    public SetSmsOptInValidator() =>
        When(x => x.OptIn, () => RuleFor(x => x.Mobile).Must(m => MobileNumber.TryCreate(m, out _)).WithErrorCode(NotificationErrorCodes.SmsInvalidField));
}

public sealed class GetNotificationPreferencesValidator : AbstractValidator<GetNotificationPreferencesQuery>
{
    public GetNotificationPreferencesValidator() =>
        RuleFor(x => x.Channel).Must(c => c is "email" or "in-app" or "sms").WithErrorCode("VAL.Channel.Invalid");
}

public sealed class UnsubscribeValidator : AbstractValidator<UnsubscribeCommand>
{
    public UnsubscribeValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(500).WithErrorCode("VAL.Token.Required");
}

internal sealed class PreferenceHandlers :
    ICommandHandler<SetEmailPreferenceCommand, NotificationPreferencesDto>,
    ICommandHandler<SetInAppPreferenceCommand, NotificationPreferencesDto>,
    ICommandHandler<SetSmsOptInCommand, NotificationPreferencesDto>,
    IQueryHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>,
    ICommandHandler<UnsubscribeCommand, UnsubscribeResult>
{
    private readonly INotificationPreferenceRepository _repository;
    private readonly IUnsubscribeTokens _tokens;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PreferenceHandlers(INotificationPreferenceRepository repository, IUnsubscribeTokens tokens, ICurrentUser user, TimeProvider clock)
    {
        _repository = repository;
        _tokens = tokens;
        _user = user;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private Actor Me => new(_user.UserId!.Value, false);

    public async Task<Result<NotificationPreferencesDto>> Handle(SetEmailPreferenceCommand request, CancellationToken ct)
    {
        var preference = await Load(Me.Id, ct);
        preference.SetEmailPreference(Me, request.Categories, Enum.Parse<DeliveryMode>(request.Mode, true), Now);
        return ToDto(preference, "email");
    }

    public async Task<Result<NotificationPreferencesDto>> Handle(SetInAppPreferenceCommand request, CancellationToken ct)
    {
        var preference = await Load(Me.Id, ct);
        preference.SetInAppPreference(Me, request.Categories, Now);
        return ToDto(preference, "in-app");
    }

    public async Task<Result<NotificationPreferencesDto>> Handle(SetSmsOptInCommand request, CancellationToken ct)
    {
        var preference = await Load(Me.Id, ct);
        preference.SetSmsOptIn(Me, request.Mobile, request.OptIn, Now);
        return ToDto(preference, "sms");
    }

    public async Task<Result<NotificationPreferencesDto>> Handle(GetNotificationPreferencesQuery request, CancellationToken ct) =>
        ToDto(await _repository.GetAsync(Me.Id, ct) ?? NotificationPreference.Default(Me.Id, Now), request.Channel);

    public async Task<Result<UnsubscribeResult>> Handle(UnsubscribeCommand request, CancellationToken ct)
    {
        if (!_tokens.TryParse(request.Token, out var accountId, out var category))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["token"] = new[] { "VAL.Token.Invalid" } }, NotificationErrorCodes.InvalidToken,
                "The unsubscribe link is invalid.");
        }

        var preference = await Load(accountId, ct);
        preference.Unsubscribe(category, Now);
        return new UnsubscribeResult(category);
    }

    /// <summary>Loads or starts the user's preferences. Saves are last-write-wins (no optimistic-concurrency token, AC-04).</summary>
    private async Task<NotificationPreference> Load(Guid accountId, CancellationToken ct)
    {
        var preference = await _repository.GetAsync(accountId, ct);
        if (preference is null)
        {
            preference = NotificationPreference.Default(accountId, Now);
            _repository.Add(preference);
        }

        return preference;
    }

    private static NotificationPreferencesDto ToDto(NotificationPreference p, string channel)
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
