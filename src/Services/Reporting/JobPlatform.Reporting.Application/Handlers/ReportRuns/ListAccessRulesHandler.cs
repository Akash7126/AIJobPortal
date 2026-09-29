using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Application.Queries.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class ListAccessRulesHandler : IQueryHandler<ListAccessRulesQuery, IReadOnlyList<AccessRuleDto>>
{
    private readonly IReportAccessRuleRepository _rules;

    public ListAccessRulesHandler(IReportAccessRuleRepository rules) => _rules = rules;

    public async Task<Result<IReadOnlyList<AccessRuleDto>>> Handle(ListAccessRulesQuery request, CancellationToken ct) =>
        (await _rules.ListAsync(ct)).Select(ReportRunService.ToDto).ToList();
}
