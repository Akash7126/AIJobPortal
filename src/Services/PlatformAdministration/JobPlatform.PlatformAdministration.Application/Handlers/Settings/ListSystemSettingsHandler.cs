using JobPlatform.PlatformAdministration.Application.DTOs.Settings;
using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Application.Queries.Settings;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Settings;

internal sealed class ListSystemSettingsHandler : IQueryHandler<ListSystemSettingsQuery, IReadOnlyList<SettingView>>
{
    private readonly IAdminReadStore _store;

    public ListSystemSettingsHandler(IAdminReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<SettingView>>> Handle(ListSystemSettingsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListSettingsAsync(ct));
}
