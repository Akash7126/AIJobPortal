using JobPlatform.Reporting.Application.Commands.Activity;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Services.Activity;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Activity;

internal sealed class RunRetentionJobHandler : ICommandHandler<RunRetentionJobCommand, int>
{
    private readonly IRetentionPolicyRepository _policies;
    private readonly IFactStore _facts;
    private readonly ReportingOptionsAccessor _options;
    private readonly IPerformanceAlertRuleRepository _alerts;
    private readonly ActivityService _activityService;

    public RunRetentionJobHandler(IRetentionPolicyRepository policies, IFactStore facts, ReportingOptionsAccessor options, IPerformanceAlertRuleRepository alerts, ActivityService activityService)
    {
        _policies = policies;
        _facts = facts;
        _options = options;
        _alerts = alerts;
        _activityService = activityService;
    }

    public async Task<Result<int>> Handle(RunRetentionJobCommand request, CancellationToken ct)
    {
        var policy = await _policies.GetAsync(ct);
        var months = policy?.RetentionMonths ?? Math.Max(ActivityLogRetentionPolicy.DefaultMonths, _options.Value.LegalMinimumRetentionMonths);
        var deleted = await _facts.DeleteEventsBeforeAsync(_activityService.Now.AddMonths(-months), request.BatchSize, ct);
        await _facts.DeleteHistoryBeforeAsync(_activityService.Now.AddMonths(-12), ct);
        await _alerts.PurgeExpiredAlertsAsync(_activityService.Now, ct);
        return deleted;
    }
}
