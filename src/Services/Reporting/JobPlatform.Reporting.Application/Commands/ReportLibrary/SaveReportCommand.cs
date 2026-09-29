using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.ReportLibrary;

public sealed record SaveReportCommand(string Name, ReportDefinitionDto Definition) : CustomCommandRequest, ICommand<SavedReportDto>;
