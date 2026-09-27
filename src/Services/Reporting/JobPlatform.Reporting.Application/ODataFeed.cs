using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application;

/// <summary>
/// Read-only views for the Power BI feed (handover 6.1, Q-10: OData read-only views, service principal). The views are the built-in data sources with all their
/// fields, already aggregated and small-cell suppressed. Access: an internal service principal (System actor); administrators use the report endpoints instead.
/// </summary>
public sealed record GetODataViewQuery(string View, int Top) : IQuery<ReportTable>, IAuthorizedRequest
{
    public static readonly IReadOnlyDictionary<string, ReportDataSource> Views = new Dictionary<string, ReportDataSource>(StringComparer.OrdinalIgnoreCase)
    {
        ["ActivityDaily"] = ReportDataSource.Activity,
        ["JobPostings"] = ReportDataSource.Employment,
        ["SystemMetrics"] = ReportDataSource.Performance
    };

    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };

    public string ForbiddenErrorCode => ReportingErrorCodes.CustomForbidden;
}

public sealed class GetODataViewValidator : AbstractValidator<GetODataViewQuery>
{
    public GetODataViewValidator()
    {
        RuleFor(x => x.View).Must(v => GetODataViewQuery.Views.ContainsKey(v)).WithErrorCode("VAL.View.Unknown");
        RuleFor(x => x.Top).InclusiveBetween(1, 10_000).WithErrorCode("VAL.Top.OutOfRange");
    }
}

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
