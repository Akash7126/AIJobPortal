using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.ReportLibrary;

// US-3.5.4-02 templates and US-3.5.4-07 saved report library.

public sealed record ListTemplatesQuery : CustomRequest, IQuery<IReadOnlyList<TemplateDto>>;
