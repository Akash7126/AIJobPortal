using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Application.Queries.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class ListTemplatesHandler : IQueryHandler<ListTemplatesQuery, IReadOnlyList<TemplateDto>>
{
    private readonly IReportTemplateRepository _templates;
    private readonly ReportLibraryService _reportLibraryService;

    public ListTemplatesHandler(IReportTemplateRepository templates, ReportLibraryService reportLibraryService)
    {
        _templates = templates;
        _reportLibraryService = reportLibraryService;
    }

    public async Task<Result<IReadOnlyList<TemplateDto>>> Handle(ListTemplatesQuery request, CancellationToken ct) =>
        await _reportLibraryService.DeniedAsync(nameof(ListTemplatesQuery), ct) is { } denied ? denied : (await _templates.ListAsync(ct)).Select(ReportMapping.ToDto).ToList();
}
