namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record JobStatusHistoryDto(Guid JobPostingId, Guid EmployerId, string? FromStatus, string ToStatus, string? Reason, DateTime ChangedAtUtc);
