using JobPlatform.Reporting.Application.DTOs.ReportLibrary;
using JobPlatform.Reporting.Application.Queries.ReportLibrary;
using JobPlatform.Reporting.Application.Services.ReportLibrary;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportLibrary;

internal sealed class ListSavedReportsHandler : IQueryHandler<ListSavedReportsQuery, IReadOnlyList<SavedReportDto>>
{
    private readonly ISavedReportRepository _saved;
    private readonly ICurrentUser _user;
    private readonly ReportLibraryService _reportLibraryService;

    public ListSavedReportsHandler(ISavedReportRepository saved, ICurrentUser user, ReportLibraryService reportLibraryService)
    {
        _saved = saved;
        _user = user;
        _reportLibraryService = reportLibraryService;
    }

    /// <summary>Default visibility is the caller's own reports (A-02-015).</summary>
    public async Task<Result<IReadOnlyList<SavedReportDto>>> Handle(ListSavedReportsQuery request, CancellationToken ct) =>
        await _reportLibraryService.DeniedAsync(nameof(ListSavedReportsQuery), ct) is { } denied
            ? denied
            : (await _saved.ListOwnedAsync(_user.UserId!.Value, ct)).Select(ReportMapping.ToDto).ToList();
}
