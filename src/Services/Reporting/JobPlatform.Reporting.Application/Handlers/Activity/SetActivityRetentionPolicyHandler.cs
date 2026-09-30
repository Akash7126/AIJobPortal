using JobPlatform.Reporting.Application.Commands.Activity;
using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Services.Activity;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Activity;

internal sealed class SetActivityRetentionPolicyHandler : ICommandHandler<SetActivityRetentionPolicyCommand, RetentionPolicyDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly IRetentionPolicyRepository _policies;
    private readonly ICurrentUser _user;
    private readonly ReportingOptionsAccessor _options;
    private readonly ActivityService _activityService;

    public SetActivityRetentionPolicyHandler(IReportAccessGuard guard, IRetentionPolicyRepository policies, ICurrentUser user, ReportingOptionsAccessor options, ActivityService activityService)
    {
        _guard = guard;
        _policies = policies;
        _user = user;
        _options = options;
        _activityService = activityService;
    }

    public async Task<Result<RetentionPolicyDto>> Handle(SetActivityRetentionPolicyCommand request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.ActivityLogs, nameof(SetActivityRetentionPolicyCommand), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var policy = await _policies.GetAsync(ct);
        if (policy is null)
        {
            policy = ActivityLogRetentionPolicy.CreateDefault(_options.Value.LegalMinimumRetentionMonths, _activityService.Now);
            _policies.Add(policy);
        }
        else if (policy.LegalMinimumMonths != _options.Value.LegalMinimumRetentionMonths)
        {
            policy.UpdateLegalMinimum(_options.Value.LegalMinimumRetentionMonths);
        }

        policy.Set(request.Months, _user.UserId!.Value, _activityService.Now);
        return ActivityService.ToDto(policy);
    }
}
