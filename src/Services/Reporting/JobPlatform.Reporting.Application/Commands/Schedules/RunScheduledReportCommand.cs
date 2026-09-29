using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Schedules;

/// <summary>Worker command: generates the report of one due schedule and requests its distribution. Not exposed over HTTP.</summary>
public sealed record RunScheduledReportCommand(Guid ScheduleId) : ICommand<bool>;
