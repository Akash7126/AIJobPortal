using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class ArchiveExpiredSavedReportsHandler : ICommandHandler<ArchiveExpiredSavedReportsCommand, int>
{
    private readonly ISavedReportRepository _saved;
    private readonly ReportLibraryService _reportLibraryService;

    public ArchiveExpiredSavedReportsHandler(ISavedReportRepository saved, ReportLibraryService reportLibraryService)
    {
        _saved = saved;
        _reportLibraryService = reportLibraryService;
    }

    public async Task<Result<int>> Handle(ArchiveExpiredSavedReportsCommand request, CancellationToken ct)
    {
        var expired = await _saved.ListExpiredAsync(_reportLibraryService.Now, request.BatchSize, ct);
        foreach (var report in expired)
        {
            report.Archive(_reportLibraryService.Now);
        }

        return expired.Count;
    }
}
