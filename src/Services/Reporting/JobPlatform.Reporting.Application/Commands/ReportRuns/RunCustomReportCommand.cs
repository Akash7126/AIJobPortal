using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Commands.ReportRuns;

// US-3.5.4-01 run, -03 formats/render, -06 builder, -07 run a saved report, -08 access rules.

/// <summary>Run a template or an ad-hoc definition (exactly one). Target: builtin or powerbi (falls back to builtin with E-CRG-UPSTREAM-TIMEOUT).</summary>
public sealed record RunCustomReportCommand(Guid? TemplateId, ReportDefinitionDto? Definition, IReadOnlyDictionary<string, string>? Arguments, string? Target)
    : CustomCommandRequest, ICommand<ReportResultDto>;
