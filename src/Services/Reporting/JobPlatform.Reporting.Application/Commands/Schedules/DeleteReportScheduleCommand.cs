using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.Schedules;

public sealed record DeleteReportScheduleCommand(Guid Id) : CustomCommandRequest, ICommand;
