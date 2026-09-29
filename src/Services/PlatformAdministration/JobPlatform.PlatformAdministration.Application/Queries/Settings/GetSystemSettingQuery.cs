using JobPlatform.PlatformAdministration.Application.DTOs.Settings;

namespace JobPlatform.PlatformAdministration.Application.Queries.Settings;

/// <summary>Internal read of one setting by consumers (GET /internal/v1/settings/{key}), Redis-cached for five minutes.</summary>
public sealed record GetSystemSettingQuery(string Key) : ServiceQuery<SettingView>;
