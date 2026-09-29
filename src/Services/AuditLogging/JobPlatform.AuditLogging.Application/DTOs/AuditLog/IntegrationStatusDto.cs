namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record IntegrationStatusDto(string Health, int Pending, int Synced, int Failed, int Archived, DateTime? LastSyncAtUtc, int SubmittedLast30Days);
