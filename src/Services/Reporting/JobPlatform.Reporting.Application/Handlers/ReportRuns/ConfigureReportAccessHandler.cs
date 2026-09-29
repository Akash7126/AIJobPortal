using JobPlatform.Reporting.Application.Commands.ReportRuns;
using JobPlatform.Reporting.Application.DTOs.ReportRuns;
using JobPlatform.Reporting.Application.Services.ReportRuns;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.ReportRuns;

internal sealed class ConfigureReportAccessHandler : ICommandHandler<ConfigureReportAccessCommand, AccessRuleDto>
{
    private readonly IReportAccessRuleRepository _rules;
    private readonly TimeProvider _clock;

    public ConfigureReportAccessHandler(IReportAccessRuleRepository rules, TimeProvider clock)
    {
        _rules = rules;
        _clock = clock;
    }

    public async Task<Result<AccessRuleDto>> Handle(ConfigureReportAccessCommand request, CancellationToken ct)
    {
        var categories = request.Categories.Select(c => Enum.Parse<ReportCategory>(c, true)).ToList();
        var now = _clock.GetUtcNow().UtcDateTime;
        var rule = await _rules.GetByRoleAsync(request.Role, ct);
        if (rule is null)
        {
            rule = ReportAccessRule.Create(request.Role, categories, now);
            _rules.Add(rule);
        }
        else
        {
            rule.Set(request.Role, categories, now);
        }

        return ReportRunService.ToDto(rule);
    }
}
