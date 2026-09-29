using JobPlatform.PlatformAdministration.Application.DTOs.Settings;
using JobPlatform.PlatformAdministration.Application.Queries.Settings;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Settings;

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
