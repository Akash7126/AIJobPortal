namespace JobPlatform.AccountIdentity.Application.DTOs.Administration;

public sealed record SessionTimeoutView(int IdleTimeoutMinutes, int SettingVersion, DateTime UpdatedAtUtc, string ETag = "");
