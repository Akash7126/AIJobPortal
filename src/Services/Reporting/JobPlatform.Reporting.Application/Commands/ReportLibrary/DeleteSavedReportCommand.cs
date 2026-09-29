using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.ReportLibrary;

public sealed record DeleteSavedReportCommand(Guid Id) : CustomCommandRequest, ICommand;
