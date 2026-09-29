namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record UsageDayDto(DateOnly Day, int Submitted, int Matched, int Viewed);
