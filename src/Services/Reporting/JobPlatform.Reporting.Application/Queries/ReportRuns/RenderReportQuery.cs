using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.ReportRuns;

public sealed record RenderReportQuery(Guid? TemplateId, ReportDefinitionDto? Definition, IReadOnlyDictionary<string, string>? Arguments, string Format)
    : CustomRequest, IQuery<ReportViewDto>;
