using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Reporting.Application.Queries.ReportRuns;

public sealed record GetReportBuilderFieldsQuery(string DataSource) : CustomRequest, IQuery<IReadOnlyList<BuilderFieldDto>>;
