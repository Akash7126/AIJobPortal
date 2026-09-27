using FluentValidation;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;

namespace JobPlatform.PlatformAdministration.Application.Settings;

/// <summary>US-3.1.4-06: change a named system setting. Takes effect on save; later save wins and every change is logged.</summary>
public sealed record ChangeSystemSettingCommand(string Key, string? Value) : AdminCommand<VersionResult>, ILaterSaveWinsCommand;

public sealed class ChangeSystemSettingValidator : AbstractValidator<ChangeSystemSettingCommand>
{
    public ChangeSystemSettingValidator()
    {
        RuleFor(c => c.Key).NotEmpty().WithErrorCode("VAL.Key.Required")
            .Must(key => SystemSettingCatalog.Find(key) is not null).WithErrorCode("VAL.Key.Unknown");
        RuleFor(c => c.Value).NotNull().WithErrorCode("VAL.Value.Required");
        // The value must parse to the setting's type; the bounds are a domain rule (INV-08).
        RuleFor(c => c.Value).Must((command, value) =>
                SystemSettingCatalog.Find(command.Key) is not { } definition || SettingValueParser.CanParse(definition.ValueType, value))
            .WithErrorCode("VAL.Value.InvalidType").When(c => c.Value is not null);
    }
}

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

public sealed record ListSystemSettingsQuery : AdminQuery<IReadOnlyList<SettingView>>;

internal sealed class ListSystemSettingsHandler : IQueryHandler<ListSystemSettingsQuery, IReadOnlyList<SettingView>>
{
    private readonly IAdminReadStore _store;

    public ListSystemSettingsHandler(IAdminReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<SettingView>>> Handle(ListSystemSettingsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListSettingsAsync(ct));
}

/// <summary>Internal read of one setting by consumers (GET /internal/v1/settings/{key}), Redis-cached for five minutes.</summary>
public sealed record GetSystemSettingQuery(string Key) : ServiceQuery<SettingView>;

internal sealed class GetSystemSettingHandler : IQueryHandler<GetSystemSettingQuery, SettingView>
{
    private readonly IAdminReadStore _store;
    private readonly IReferenceDataCache _cache;

    public GetSystemSettingHandler(IAdminReadStore store, IReferenceDataCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<Result<SettingView>> Handle(GetSystemSettingQuery request, CancellationToken ct)
    {
        var key = CacheKeys.Setting(request.Key);
        if (await _cache.GetAsync<SettingView>(key, ct) is { } cached)
        {
            return cached;
        }

        if (await _store.GetSettingAsync(request.Key, ct) is not { } view)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The setting was not found.");
        }

        await _cache.SetAsync(key, view, CacheKeys.SettingTtl, ct);
        return view;
    }
}
