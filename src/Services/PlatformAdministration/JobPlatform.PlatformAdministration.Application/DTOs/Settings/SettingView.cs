namespace JobPlatform.PlatformAdministration.Application.DTOs.Settings;

public sealed record SettingView(string Key, string ValueType, string Value, int Version, SettingBoundsView Bounds, Guid? UpdatedBy, DateTime UpdatedAtUtc);
