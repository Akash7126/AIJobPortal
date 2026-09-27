using FluentValidation;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.Reporting.Application;

// Module A - user activity monitoring (US-3.5.1-01..03) plus the scheduled maintenance commands (retention, rollup rebuild).

public sealed record GetUserActivityQuery(DateOnly? From, DateOnly? To, string? Type) : ActivityRequest, IQuery<UserActivityDto>;

public sealed record GetRetentionPolicyQuery : ActivityRequest, IQuery<RetentionPolicyDto>;

public sealed record SetActivityRetentionPolicyCommand(int Months) : ActivityCommandRequest, ICommand<RetentionPolicyDto>;

public sealed record GetLoginDashboardQuery : ActivityRequest, IQuery<LoginDashboardDto>;

/// <summary>Scheduled (leader-locked): removes activity facts past the retention period (AC-04). Not exposed over HTTP.</summary>
public sealed record RunRetentionJobCommand(int BatchSize) : ICommand<int>;

/// <summary>Ops command: rebuilds every daily rollup from the fact table. Not exposed over HTTP.</summary>
public sealed record RebuildRollupsCommand : ICommand<int>;

public sealed class GetUserActivityValidator : AbstractValidator<GetUserActivityQuery>
{
    public GetUserActivityValidator()
    {
        DateRangeRules.AddTo(this, x => x.From, x => x.To);
        RuleFor(x => x.Type).Must(t => t is null || ActivityTypes.All.Contains(t, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Type.Unknown");
    }
}

public sealed class SetActivityRetentionPolicyValidator : AbstractValidator<SetActivityRetentionPolicyCommand>
{
    public SetActivityRetentionPolicyValidator() =>
        RuleFor(x => x.Months).InclusiveBetween(1, ActivityLogRetentionPolicy.MaxMonths).WithErrorCode("VAL.Months.OutOfRange");
}

internal sealed class ActivityHandlers :
    IQueryHandler<GetUserActivityQuery, UserActivityDto>,
    IQueryHandler<GetRetentionPolicyQuery, RetentionPolicyDto>,
    ICommandHandler<SetActivityRetentionPolicyCommand, RetentionPolicyDto>,
    IQueryHandler<GetLoginDashboardQuery, LoginDashboardDto>,
    ICommandHandler<RunRetentionJobCommand, int>,
    ICommandHandler<RebuildRollupsCommand, int>
{
    private readonly IReportAccessGuard _guard;
    private readonly IAnalyticsQueryService _analytics;
    private readonly IRetentionPolicyRepository _policies;
    private readonly IFactStore _facts;
    private readonly ISessionSource _sessions;
    private readonly ICurrentUser _user;
    private readonly ReportingOptionsAccessor _options;
    private readonly IPerformanceAlertRuleRepository _alerts;
    private readonly TimeProvider _clock;

    public ActivityHandlers(IReportAccessGuard guard, IAnalyticsQueryService analytics, IRetentionPolicyRepository policies, IFactStore facts, ISessionSource sessions,
        ICurrentUser user, ReportingOptionsAccessor options, IPerformanceAlertRuleRepository alerts, TimeProvider clock)
    {
        _guard = guard;
        _analytics = analytics;
        _policies = policies;
        _facts = facts;
        _sessions = sessions;
        _user = user;
        _options = options;
        _alerts = alerts;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<Result<UserActivityDto>> Handle(GetUserActivityQuery request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.ActivityLogs, nameof(GetUserActivityQuery), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var range = DateRanges.Resolve(request.From, request.To, _clock);
        var counts = await _analytics.DailyCountsAsync("activity.", range.From, range.To, ct);
        var byType = counts.GroupBy(c => c.Metric["activity.".Length..]).ToDictionary(g => g.Key, g => g.Sum(c => c.Count), StringComparer.OrdinalIgnoreCase);
        var types = request.Type is null ? ActivityTypes.All : ActivityTypes.All.Where(t => t.Equals(request.Type, StringComparison.OrdinalIgnoreCase)).ToArray();
        var items = types.Select(t => new ActivityCountDto(t, byType.GetValueOrDefault(t))).ToList();
        return new UserActivityDto(range.From, range.To, items.Sum(i => i.Count), items);
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
            ? new RetentionPolicyDto(Math.Max(ActivityLogRetentionPolicy.DefaultMonths, _options.Value.LegalMinimumRetentionMonths), _options.Value.LegalMinimumRetentionMonths, Now)
            : ToDto(policy);
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
            policy = ActivityLogRetentionPolicy.CreateDefault(_options.Value.LegalMinimumRetentionMonths, Now);
            _policies.Add(policy);
        }
        else if (policy.LegalMinimumMonths != _options.Value.LegalMinimumRetentionMonths)
        {
            policy.UpdateLegalMinimum(_options.Value.LegalMinimumRetentionMonths);
        }

        policy.Set(request.Months, _user.UserId!.Value, Now);
        return ToDto(policy);
    }

    public async Task<Result<LoginDashboardDto>> Handle(GetLoginDashboardQuery request, CancellationToken ct)
    {
        var access = await _guard.EnsureAsync(ReportCategory.ActivityLogs, nameof(GetLoginDashboardQuery), ct);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var sessions = await _sessions.GetActiveAsync(ct);
        return sessions is null
            ? new LoginDashboardDto(false, 0, null, Array.Empty<ActiveSession>())
            : new LoginDashboardDto(true, sessions.Count, sessions.Count == 0 ? null : sessions.Max(s => s.LastSeenAtUtc), sessions.OrderByDescending(s => s.LastSeenAtUtc).ToList());
    }

    public async Task<Result<int>> Handle(RunRetentionJobCommand request, CancellationToken ct)
    {
        var policy = await _policies.GetAsync(ct);
        var months = policy?.RetentionMonths ?? Math.Max(ActivityLogRetentionPolicy.DefaultMonths, _options.Value.LegalMinimumRetentionMonths);
        var deleted = await _facts.DeleteEventsBeforeAsync(Now.AddMonths(-months), request.BatchSize, ct);
        await _facts.DeleteHistoryBeforeAsync(Now.AddMonths(-12), ct);
        await _alerts.PurgeExpiredAlertsAsync(Now, ct);
        return deleted;
    }

    public async Task<Result<int>> Handle(RebuildRollupsCommand request, CancellationToken ct) => await _facts.RebuildRollupsAsync(ct);

    private static RetentionPolicyDto ToDto(ActivityLogRetentionPolicy p) => new(p.RetentionMonths, p.LegalMinimumMonths, p.UpdatedAtUtc);
}

/// <summary>Scoped accessor so handlers read the current options without depending on the options infrastructure.</summary>
public sealed class ReportingOptionsAccessor
{
    private readonly Microsoft.Extensions.Options.IOptions<ReportingOptions> _options;

    public ReportingOptionsAccessor(Microsoft.Extensions.Options.IOptions<ReportingOptions> options) => _options = options;

    public ReportingOptions Value => _options.Value;
}
