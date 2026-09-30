using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.ReportRuns;

public sealed record GetReportFormatsQuery : CustomRequest, IQuery<IReadOnlyList<string>>;
