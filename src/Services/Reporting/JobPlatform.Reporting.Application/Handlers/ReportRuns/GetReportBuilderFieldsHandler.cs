using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Application.Queries.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class GetReportBuilderFieldsHandler : IQueryHandler<GetReportBuilderFieldsQuery, IReadOnlyList<BuilderFieldDto>>
{
    private readonly ReportRunService _reportRunService;

    public GetReportBuilderFieldsHandler(ReportRunService reportRunService) => _reportRunService = reportRunService;

    public async Task<Result<IReadOnlyList<BuilderFieldDto>>> Handle(GetReportBuilderFieldsQuery request, CancellationToken ct)
    {
        var source = Enum.Parse<ReportDataSource>(request.DataSource, true);
        if (await _reportRunService.DeniedAsync(ReportCatalog.CategoryOf(source), nameof(GetReportBuilderFieldsQuery), ct) is { } denied)
        {
            return denied;
        }

        return ReportCatalog.FieldsOf(source).Select(f => new BuilderFieldDto(f.Name, f.Kind.ToString(), f.Aggregation.ToString())).ToList();
    }
}
