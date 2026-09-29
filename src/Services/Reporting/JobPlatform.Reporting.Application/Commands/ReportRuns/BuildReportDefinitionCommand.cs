using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.ReportRuns;

public sealed record BuildReportDefinitionCommand(ReportDefinitionDto Definition) : CustomCommandRequest, ICommand<ReportDefinitionDto>;
