using JobPlatform.PlatformAdministration.Application.DTOs.Settings;

namespace JobPlatform.PlatformAdministration.Application.Queries.Settings;

public sealed record ListSystemSettingsQuery : AdminQuery<IReadOnlyList<SettingView>>;
