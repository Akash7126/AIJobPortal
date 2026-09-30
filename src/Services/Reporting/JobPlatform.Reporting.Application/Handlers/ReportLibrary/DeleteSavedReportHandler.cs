using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class DeleteSavedReportHandler : ICommandHandler<DeleteSavedReportCommand, Unit>
{
    private readonly ISavedReportRepository _saved;
    private readonly ICurrentUser _user;
    private readonly ReportLibraryService _reportLibraryService;

    public DeleteSavedReportHandler(ISavedReportRepository saved, ICurrentUser user, ReportLibraryService reportLibraryService)
    {
        _saved = saved;
        _user = user;
        _reportLibraryService = reportLibraryService;
    }

    public async Task<Result<Unit>> Handle(DeleteSavedReportCommand request, CancellationToken ct)
    {
        if (await _reportLibraryService.DeniedAsync(nameof(DeleteSavedReportCommand), ct) is { } denied)
        {
            return denied;
        }

        var report = await _saved.GetAsync(request.Id, ct);
        if (report is null)
        {
            return Error.NotFound(ReportingErrorCodes.NotFound, "The saved report was not found.");
        }

        report.EnsureOwnedBy(_user.UserId!.Value);
        _saved.Remove(report);
        return Result.Success();
    }
}
