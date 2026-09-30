using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class ConfigureSessionTimeoutHandler : ICommandHandler<ConfigureSessionTimeoutCommand, Unit>
{
    private readonly ISessionTimeoutSettingRepository _settings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ConfigureSessionTimeoutHandler(ISessionTimeoutSettingRepository settings, ICurrentUser user, TimeProvider clock)
    {
        _settings = settings;
        _user = user;
        _clock = clock;
    }

    /// <summary>Only sessions created afterwards use the new value: existing sessions captured theirs at creation (AC-03).</summary>
    public async Task<Result<Unit>> Handle(ConfigureSessionTimeoutCommand request, CancellationToken ct)
    {
        var setting = await _settings.GetAsync(ct);
        if (!ETag.Matches(request.IfMatch, setting.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        setting.Configure(Actor.Administrator(_user.UserId!.Value), request.IdleTimeoutMinutes, _clock);
        return Result.Success();
    }
}
