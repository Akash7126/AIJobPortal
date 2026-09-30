using JobPlatform.Reporting.Application.Queries.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class GetReportFormatsHandler : IQueryHandler<GetReportFormatsQuery, IReadOnlyList<string>>
{
    private readonly ReportRunService _reportRunService;

    public GetReportFormatsHandler(ReportRunService reportRunService) => _reportRunService = reportRunService;

    public async Task<Result<IReadOnlyList<string>>> Handle(GetReportFormatsQuery request, CancellationToken ct) =>
        await _reportRunService.DeniedAsync(ReportCategory.Custom, nameof(GetReportFormatsQuery), ct) is { } denied ? denied : ReportViewBuilder.Formats.ToList();
}
