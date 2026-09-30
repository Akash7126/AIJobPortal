using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Application.Interfaces;
using JobPlatform.Notification.Application.Services.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Preferences;

internal sealed class UnsubscribeHandler : ICommandHandler<UnsubscribeCommand, UnsubscribeResult>
{
    private readonly IUnsubscribeTokens _tokens;
    private readonly PreferenceService _preferenceService;

    public UnsubscribeHandler(IUnsubscribeTokens tokens, PreferenceService preferenceService)
    {
        _tokens = tokens;
        _preferenceService = preferenceService;
    }

    public async Task<Result<UnsubscribeResult>> Handle(UnsubscribeCommand request, CancellationToken ct)
    {
        if (!_tokens.TryParse(request.Token, out var accountId, out var category))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["token"] = new[] { "VAL.Token.Invalid" } }, NotificationErrorCodes.InvalidToken,
                "The unsubscribe link is invalid.");
        }

        var preference = await _preferenceService.Load(accountId, ct);
        preference.Unsubscribe(category, _preferenceService.Now);
        return new UnsubscribeResult(category);
    }
}
