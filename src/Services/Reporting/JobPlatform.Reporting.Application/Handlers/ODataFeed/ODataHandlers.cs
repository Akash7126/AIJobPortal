using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Application.Queries.ODataFeed;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ODataFeed;

internal sealed class ODataHandlers : IQueryHandler<GetODataViewQuery, ReportTable>
{
    private readonly ReportEngine _engine;

    public ODataHandlers(ReportEngine engine) => _engine = engine;

    public async Task<Result<ReportTable>> Handle(GetODataViewQuery request, CancellationToken ct)
    {
        var source = GetODataViewQuery.Views[request.View];
        var fields = ReportCatalog.FieldsOf(source).Select(f => f.Name).ToArray();
        var table = await _engine.RunAsync(new ReportDefinition(source, fields, Array.Empty<ReportFilter>(), Array.Empty<string>()), null, ct);
        return table with { Rows = table.Rows.Take(request.Top).ToList() };
    }
}
