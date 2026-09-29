using JobPlatform.PlatformAdministration.Application.DTOs.Common;

namespace JobPlatform.PlatformAdministration.Application.Commands.Settings;

/// <summary>US-3.1.4-06: change a named system setting. Takes effect on save; later save wins and every change is logged.</summary>
public sealed record ChangeSystemSettingCommand(string Key, string? Value) : AdminCommand<VersionResult>, ILaterSaveWinsCommand;
