using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.ReportRuns;

public sealed record GetReportFormatsQuery : CustomRequest, IQuery<IReadOnlyList<string>>;
