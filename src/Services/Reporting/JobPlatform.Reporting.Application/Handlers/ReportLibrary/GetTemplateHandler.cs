using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Application.Queries.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class GetTemplateHandler : IQueryHandler<GetTemplateQuery, TemplateDto>
{
    private readonly IReportTemplateRepository _templates;
    private readonly ReportLibraryService _reportLibraryService;

    public GetTemplateHandler(IReportTemplateRepository templates, ReportLibraryService reportLibraryService)
    {
        _templates = templates;
        _reportLibraryService = reportLibraryService;
    }

    public async Task<Result<TemplateDto>> Handle(GetTemplateQuery request, CancellationToken ct)
    {
        if (await _reportLibraryService.DeniedAsync(nameof(GetTemplateQuery), ct) is { } denied)
        {
            return denied;
        }

        var template = await _templates.GetAsync(request.Id, ct);
        return template is null ? Error.NotFound(ReportingErrorCodes.NotFound, "The template was not found.") : ReportMapping.ToDto(template);
    }
}
