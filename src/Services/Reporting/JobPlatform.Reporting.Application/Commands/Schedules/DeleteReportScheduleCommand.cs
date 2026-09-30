using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.Schedules;

public sealed record DeleteReportScheduleCommand(Guid Id) : CustomCommandRequest, ICommand;
