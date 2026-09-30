using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class SaveReportTemplateHandler : ICommandHandler<SaveReportTemplateCommand, TemplateDto>
{
    private readonly IReportTemplateRepository _templates;
    private readonly ICurrentUser _user;
    private readonly ReportLibraryService _reportLibraryService;

    public SaveReportTemplateHandler(IReportTemplateRepository templates, ICurrentUser user, ReportLibraryService reportLibraryService)
    {
        _templates = templates;
        _user = user;
        _reportLibraryService = reportLibraryService;
    }

    public async Task<Result<TemplateDto>> Handle(SaveReportTemplateCommand request, CancellationToken ct)
    {
        if (await _reportLibraryService.DeniedAsync(nameof(SaveReportTemplateCommand), ct) is { } denied)
        {
            return denied;
        }

        var source = Enum.Parse<ReportDataSource>(request.DataSource, true);
        var parameters = (request.Parameters ?? Array.Empty<TemplateParameterDto>()).Select(ReportMapping.ToDomain).ToList();
        ReportTemplate template;
        if (request.Id is { } id)
        {
            var existing = await _templates.GetAsync(id, ct);
            if (existing is null)
            {
                return Error.NotFound(ReportingErrorCodes.NotFound, "The template was not found.");
            }

            existing.Save(request.Name, source, parameters, _reportLibraryService.Now);
            template = existing;
        }
        else
        {
            template = ReportTemplate.Create(request.Name, source, parameters, _user.UserId!.Value, _reportLibraryService.Now);
            _templates.Add(template);
        }

        return ReportMapping.ToDto(template);
    }
}
