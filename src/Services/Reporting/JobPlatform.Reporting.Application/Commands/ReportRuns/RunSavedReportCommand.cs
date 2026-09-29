using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.ReportRuns;

public sealed record RunSavedReportCommand(Guid Id, IReadOnlyDictionary<string, string>? Arguments, string? Target) : CustomCommandRequest, ICommand<ReportResultDto>;
