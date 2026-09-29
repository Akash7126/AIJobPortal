namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record UsageStatisticsDto(DateOnly From, DateOnly To, int Submitted, int Matched, int Viewed, IReadOnlyList<UsageDayDto> Days);
