using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.Reporting.Application.Queries.ODataFeed;

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
