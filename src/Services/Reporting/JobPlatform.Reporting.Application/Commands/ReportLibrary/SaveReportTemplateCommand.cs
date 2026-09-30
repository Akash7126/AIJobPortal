using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Commands.ReportLibrary;

/// <summary>Creates a template (Id null) or replaces it; the later save wins (AC-04).</summary>
public sealed record SaveReportTemplateCommand(Guid? Id, string Name, string DataSource, IReadOnlyList<TemplateParameterDto>? Parameters)
    : CustomCommandRequest, ICommand<TemplateDto>;
