using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Reporting.Application.Queries.ReportRuns;

public sealed record ListAccessRulesQuery : AccessAdminRequest, IQuery<IReadOnlyList<AccessRuleDto>>;
