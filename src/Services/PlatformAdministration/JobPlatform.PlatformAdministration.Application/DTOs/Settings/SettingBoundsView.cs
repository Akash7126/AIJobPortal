namespace JobPlatform.PlatformAdministration.Application.DTOs.Settings;

public sealed record SettingBoundsView(decimal? Min, decimal? Max, IReadOnlyList<string> AllowedValues, int? MaxLength);
