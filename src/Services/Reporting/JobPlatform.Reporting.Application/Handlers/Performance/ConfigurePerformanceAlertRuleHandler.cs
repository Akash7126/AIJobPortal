using JobPlatform.Reporting.Application.Commands.Performance;
using JobPlatform.Reporting.Application.DTOs.Performance;
using JobPlatform.Reporting.Application.Services.Performance;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Performance;

internal sealed class ConfigurePerformanceAlertRuleHandler : ICommandHandler<ConfigurePerformanceAlertRuleCommand, AlertRuleDto>
{
    private readonly IPerformanceAlertRuleRepository _rules;
    private readonly TimeProvider _clock;
    private readonly PerformanceService _performanceService;

    public ConfigurePerformanceAlertRuleHandler(IPerformanceAlertRuleRepository rules, TimeProvider clock, PerformanceService performanceService)
    {
        _rules = rules;
        _clock = clock;
        _performanceService = performanceService;
    }

    public async Task<Result<AlertRuleDto>> Handle(ConfigurePerformanceAlertRuleCommand request, CancellationToken ct)
    {
        if (await _performanceService.DeniedAsync(nameof(ConfigurePerformanceAlertRuleCommand), ct) is { } denied)
        {
            return denied;
        }

        var comparator = Enum.Parse<AlertComparator>(request.Comparator, true);
        var severity = Enum.Parse<AlertSeverity>(request.Severity, true);
        var now = _clock.GetUtcNow().UtcDateTime;
        PerformanceAlertRule? rule = null;
        if (request.Id is { } id)
        {
            rule = await _rules.GetAsync(id, ct);
            if (rule is null)
            {
                return Error.NotFound(ReportingErrorCodes.NotFound, "The alert rule was not found.");
            }

            rule.Configure(request.Metric, comparator, request.Threshold, request.WindowMinutes, severity, request.Enabled, now);
        }
        else
        {
            rule = PerformanceAlertRule.Create(request.Metric, comparator, request.Threshold, request.WindowMinutes, severity, request.Enabled, now);
            _rules.Add(rule);
        }

        return PerformanceService.ToDto(rule);
    }
}
