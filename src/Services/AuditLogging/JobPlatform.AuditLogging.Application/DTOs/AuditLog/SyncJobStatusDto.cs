namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record SyncJobStatusDto(string PlatformJobId, string Status, string? ReasonCode, DateTime UpdatedAtUtc);
