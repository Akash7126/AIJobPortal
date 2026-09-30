using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class SaveReportHandler : ICommandHandler<SaveReportCommand, SavedReportDto>
{
    private readonly ISavedReportRepository _saved;
    private readonly ICurrentUser _user;
    private readonly ReportLibraryService _reportLibraryService;

    public SaveReportHandler(ISavedReportRepository saved, ICurrentUser user, ReportLibraryService reportLibraryService)
    {
        _saved = saved;
        _user = user;
        _reportLibraryService = reportLibraryService;
    }

    public async Task<Result<SavedReportDto>> Handle(SaveReportCommand request, CancellationToken ct)
    {
        if (await _reportLibraryService.DeniedAsync(nameof(SaveReportCommand), ct) is { } denied)
        {
            return denied;
        }

        var report = SavedReport.Save(_user.UserId!.Value, request.Name, ReportMapping.ToDomain(request.Definition), _reportLibraryService.Now);
        _saved.Add(report);
        return ReportMapping.ToDto(report);
    }
}
