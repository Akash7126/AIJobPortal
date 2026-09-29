namespace JobPlatform.Reporting.Application.DTOs.Activity;

public sealed record LoginDashboardDto(bool SourceAvailable, int CurrentCount, DateTime? LastSessionAtUtc, IReadOnlyList<ActiveSession> Sessions);
