using JobPlatform.PlatformAdministration.Application.Commands.Settings;
using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Settings;

internal sealed class ChangeSystemSettingHandler : ICommandHandler<ChangeSystemSettingCommand, VersionResult>
{
    private readonly ISystemSettingRepository _settings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;
    private readonly ILogger<ChangeSystemSettingHandler> _logger;

    public ChangeSystemSettingHandler(ISystemSettingRepository settings, ICurrentUser user, TimeProvider clock, ILogger<ChangeSystemSettingHandler> logger)
    {
        _settings = settings;
        _user = user;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<VersionResult>> Handle(ChangeSystemSettingCommand request, CancellationToken ct)
    {
        var setting = await _settings.GetByKeyAsync(request.Key, ct);
        if (setting is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The setting was not found.");
        }

        setting.Change(request.Value, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _logger.LogInformation("System setting {Key} changed to version {Version} by {ActorId}", setting.Key, setting.SettingVersion, _user.UserId);
        return new VersionResult(setting.SettingVersion);
    }
}
