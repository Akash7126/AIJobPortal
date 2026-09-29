namespace JobPlatform.Reporting.Application.DTOs.Schedules;

public sealed record ScheduleDto(Guid Id, string Name, Guid? TemplateId, Guid? SavedReportId, string Interval, string? Cron, IReadOnlyList<string> Recipients, string Format,
    DateTime NextRunAtUtc, DateTime? LastRunAtUtc, bool Active);
