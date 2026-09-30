using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.ReportLibrary;

public sealed record DeleteSavedReportCommand(Guid Id) : CustomCommandRequest, ICommand;
