using JobPlatform.Reporting.Application.DTOs.Schedules;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Schedules;

/// <summary>Creates a schedule (Id null) or reconfigures it. Interval defaults to Daily (A-02-013).</summary>
public sealed record ConfigureReportScheduleCommand(Guid? Id, string Name, Guid? TemplateId, Guid? SavedReportId, string? Interval, string? Cron, IReadOnlyList<string> Recipients,
    string Format) : CustomCommandRequest, ICommand<ScheduleDto>;
