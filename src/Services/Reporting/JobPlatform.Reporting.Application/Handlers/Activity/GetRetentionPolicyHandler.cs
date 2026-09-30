using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Activity;
using JobPlatform.Reporting.Application.Services.Activity;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Activity;

internal sealed class GetRetentionPolicyHandler : IQueryHandler<GetRetentionPolicyQuery, RetentionPolicyDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly IRetentionPolicyRepository _policies;
    private readonly ReportingOptionsAccessor _options;
    private readonly ActivityService _activityService;

    public GetRetentionPolicyHandler(IReportAccessGuard guard, IRetentionPolicyRepository policies, ReportingOptionsAccessor options, ActivityService activityService)
    {
        _guard = guard;
        _policies = policies;
        _options = options;
        _activityService = activityService;
    }

    public async Task<Result<RetentionPolicyDto>> Handle(GetRetentionPolicyQuery request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.ActivityLogs, nameof(GetRetentionPolicyQuery), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var policy = await _policies.GetAsync(ct);
        return policy is null
            ? new RetentionPolicyDto(Math.Max(ActivityLogRetentionPolicy.DefaultMonths, _options.Value.LegalMinimumRetentionMonths), _options.Value.LegalMinimumRetentionMonths, _activityService.Now)
            : ActivityService.ToDto(policy);
    }
}
