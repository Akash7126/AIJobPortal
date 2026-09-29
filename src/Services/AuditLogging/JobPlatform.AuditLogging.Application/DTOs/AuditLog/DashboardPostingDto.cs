namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record DashboardPostingDto(Guid JobPostingId, string Title, string Status, DateTime UpdatedAtUtc);
