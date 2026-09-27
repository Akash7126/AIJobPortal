using FluentValidation;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application;

// ---------------------------------------------------------------------- list queries over AuditEntry (handover 6.1)

/// <summary>Common filter of the log queries: optional time window, outcome class (success | failure | duplicate) and paging.</summary>
public interface IFilteredListQuery
{
    DateTime? From { get; }
    DateTime? To { get; }
    string? Outcome { get; }
    int Page { get; }
    int PageSize { get; }
}

public sealed record ListApiResponseLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

public sealed record ListSubmissionLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

public sealed record ListSyncErrorLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

public sealed record ListAdminAuditLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : AdminRequest(AuditErrorCodes.AdminForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

public sealed record ListAccessLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : AdminRequest(AuditErrorCodes.AccessForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

public sealed record ListGovernmentDataAuditTrailQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : AdminRequest(AuditErrorCodes.GovernmentForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

public sealed record GetJobAuditTrailQuery(string PlatformJobId, DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20,
    bool IncludeArchived = false)
    : AdminRequest(AuditErrorCodes.AdminForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;

// ---------------------------------------------------------------------- notification logs (US-3.6.x)

public sealed record ListEmailLogQuery(int Page = 1, int PageSize = 20) : AdminRequest(AuditErrorCodes.EmailForbidden), IQuery<PagedResult<NotificationLogDto>>;

public sealed record ListSmsMessageLogQuery(int Page = 1, int PageSize = 20) : AdminRequest(AuditErrorCodes.SmsForbidden), IQuery<PagedResult<NotificationLogDto>>;

/// <summary>The caller's own notification history, newest first (3.6.2-04).</summary>
public sealed record ListNotificationHistoryQuery(int Page = 1, int PageSize = 20)
    : AuthenticatedRequest(AuditErrorCodes.NotificationForbidden), IQuery<PagedResult<NotificationLogDto>>;

// ---------------------------------------------------------------------- partner dashboards

public sealed record GetSyncDashboardQuery(int Page = 1, int PageSize = 20) : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden),
    IQuery<SyncDashboardDto>;

public sealed record GetIntegrationStatusDashboardQuery : ActorRequest, IQuery<IntegrationStatusDto>
{
    public GetIntegrationStatusDashboardQuery() : base(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden)
    {
    }
}

public sealed record GetIntegrationUsageStatisticsQuery(DateOnly From, DateOnly To)
    : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden), IQuery<UsageStatisticsDto>;

// ---------------------------------------------------------------------- employer views

public sealed record GetEmployerDashboardQuery : ActorRequest, IQuery<EmployerDashboardDto>
{
    public GetEmployerDashboardQuery() : base(ActorType.Employer, AuditErrorCodes.EmployerForbidden)
    {
    }
}

public sealed record GetJobStatusHistoryQuery(Guid JobPostingId) : ActorRequest(ActorType.Employer, AuditErrorCodes.JobStatusForbidden),
    IQuery<IReadOnlyList<JobStatusHistoryDto>>;

public sealed record GetCandidateInsightQuery(Guid CandidateId, Guid JobPostingId) : ActorRequest(ActorType.Employer, AuditErrorCodes.InsightForbidden),
    IQuery<CandidateInsightDto>;

// ---------------------------------------------------------------------- validators

public abstract class FilteredListValidator<TQuery> : AbstractValidator<TQuery> where TQuery : IFilteredListQuery
{
    private static readonly string[] Outcomes = { "success", "failure", "duplicate", "denied" };

    protected FilteredListValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => x.From.HasValue && x.To.HasValue, () =>
            RuleFor(x => x.To!.Value).GreaterThanOrEqualTo(x => x.From!.Value).OverridePropertyName("To").WithErrorCode("VAL.To.BeforeFrom"));
        When(x => !string.IsNullOrEmpty(x.Outcome), () =>
            RuleFor(x => x.Outcome!).Must(o => Outcomes.Contains(o.ToLowerInvariant())).OverridePropertyName("Outcome").WithErrorCode("VAL.Outcome.Invalid"));
    }
}

public sealed class ListApiResponseLogQueryValidator : FilteredListValidator<ListApiResponseLogQuery>;
public sealed class ListSubmissionLogQueryValidator : FilteredListValidator<ListSubmissionLogQuery>;
public sealed class ListSyncErrorLogQueryValidator : FilteredListValidator<ListSyncErrorLogQuery>;
public sealed class ListAdminAuditLogQueryValidator : FilteredListValidator<ListAdminAuditLogQuery>;
public sealed class ListAccessLogQueryValidator : FilteredListValidator<ListAccessLogQuery>;
public sealed class ListGovernmentDataAuditTrailQueryValidator : FilteredListValidator<ListGovernmentDataAuditTrailQuery>;

public sealed class GetJobAuditTrailQueryValidator : FilteredListValidator<GetJobAuditTrailQuery>
{
    public GetJobAuditTrailQueryValidator() => RuleFor(x => x.PlatformJobId).NotEmpty().MaximumLength(128).WithErrorCode("VAL.PlatformJobId.Required");
}

public sealed class ListEmailLogQueryValidator : AbstractValidator<ListEmailLogQuery>
{
    public ListEmailLogQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

public sealed class ListSmsMessageLogQueryValidator : AbstractValidator<ListSmsMessageLogQuery>
{
    public ListSmsMessageLogQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

public sealed class ListNotificationHistoryQueryValidator : AbstractValidator<ListNotificationHistoryQuery>
{
    public ListNotificationHistoryQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

public sealed class GetSyncDashboardQueryValidator : AbstractValidator<GetSyncDashboardQuery>
{
    public GetSyncDashboardQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}

/// <summary>Only presence is checked here; the range rule (end not before start, at most 12 months) is a domain rule reported as E-TPJPRI-INVALID-FIELD.</summary>
public sealed class GetIntegrationUsageStatisticsQueryValidator : AbstractValidator<GetIntegrationUsageStatisticsQuery>
{
    public GetIntegrationUsageStatisticsQueryValidator()
    {
        RuleFor(x => x.From).NotEqual(default(DateOnly)).WithErrorCode("VAL.From.Required");
        RuleFor(x => x.To).NotEqual(default(DateOnly)).WithErrorCode("VAL.To.Required");
    }
}

public sealed class GetJobStatusHistoryQueryValidator : AbstractValidator<GetJobStatusHistoryQuery>
{
    public GetJobStatusHistoryQueryValidator() => RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
}

public sealed class GetCandidateInsightQueryValidator : AbstractValidator<GetCandidateInsightQuery>
{
    public GetCandidateInsightQueryValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty().WithErrorCode("VAL.CandidateId.Required");
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
    }
}

// ---------------------------------------------------------------------- handlers

internal sealed class AuditLogQueryHandlers :
    IQueryHandler<ListApiResponseLogQuery, PagedResult<AuditEntryDto>>,
    IQueryHandler<ListSubmissionLogQuery, PagedResult<AuditEntryDto>>,
    IQueryHandler<ListSyncErrorLogQuery, PagedResult<AuditEntryDto>>,
    IQueryHandler<ListAdminAuditLogQuery, PagedResult<AuditEntryDto>>,
    IQueryHandler<ListAccessLogQuery, PagedResult<AuditEntryDto>>,
    IQueryHandler<ListGovernmentDataAuditTrailQuery, PagedResult<AuditEntryDto>>,
    IQueryHandler<GetJobAuditTrailQuery, PagedResult<AuditEntryDto>>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public AuditLogQueryHandlers(IAuditReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListApiResponseLogQuery q, CancellationToken ct) => Owned(AuditCategory.ApiCall, q, q.IncludeArchived, null, ct);

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListSubmissionLogQuery q, CancellationToken ct) => Owned(AuditCategory.Submission, q, q.IncludeArchived, null, ct);

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListSyncErrorLogQuery q, CancellationToken ct) => Owned(AuditCategory.SyncError, q, q.IncludeArchived, null, ct);

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListAdminAuditLogQuery q, CancellationToken ct) => Owned(AuditCategory.AdminAction, q, q.IncludeArchived, null, ct);

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListAccessLogQuery q, CancellationToken ct) => Owned(AuditCategory.Access, q, q.IncludeArchived, null, ct);

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListGovernmentDataAuditTrailQuery q, CancellationToken ct) =>
        Owned(AuditCategory.GovernmentExchange, q, q.IncludeArchived, null, ct);

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(GetJobAuditTrailQuery q, CancellationToken ct) =>
        Owned(AuditCategory.JobAudit, q, q.IncludeArchived, q.PlatformJobId, ct);

    /// <summary>Applies the scope policy for the category, then reads only what the caller may see (the owner's own rows for owned categories).</summary>
    private async Task<Result<PagedResult<AuditEntryDto>>> Owned(AuditCategory category, IFilteredListQuery q, bool includeArchived, string? subjectId, CancellationToken ct)
    {
        var viewer = new Viewer(_user.ActorType, _user.UserId);
        var ownerType = AccessScopePolicy.OwnerTypeFor(category);
        var scope = ownerType is not null && viewer.Id is { } id ? OwnerScope.Of(ownerType.Value, id) : OwnerScope.AdminOnly;
        AccessScopePolicy.EnsureCanView(category, viewer, scope);

        var outcome = string.IsNullOrEmpty(q.Outcome) ? (AuditOutcome?)null : Enum.Parse<AuditOutcome>(q.Outcome, true);
        var filter = new EntryFilter(new[] { category }, ownerType is null ? null : viewer.Id, subjectId, q.From, q.To, outcome, includeArchived);
        return await _store.ListEntriesAsync(filter, new PageRequest(q.Page, q.PageSize), ct);
    }
}

internal sealed class NotificationLogQueryHandlers :
    IQueryHandler<ListEmailLogQuery, PagedResult<NotificationLogDto>>,
    IQueryHandler<ListSmsMessageLogQuery, PagedResult<NotificationLogDto>>,
    IQueryHandler<ListNotificationHistoryQuery, PagedResult<NotificationLogDto>>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public NotificationLogQueryHandlers(IAuditReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(ListEmailLogQuery q, CancellationToken ct)
    {
        AccessScopePolicy.EnsureCanView(AuditCategory.Email, Viewer(), OwnerScope.AdminOnly);
        return await _store.ListNotificationLogAsync(new[] { "Email" }, null, new PageRequest(q.Page, q.PageSize), ct);
    }

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(ListSmsMessageLogQuery q, CancellationToken ct)
    {
        AccessScopePolicy.EnsureCanView(AuditCategory.Sms, Viewer(), OwnerScope.AdminOnly);
        return await _store.ListNotificationLogAsync(new[] { "Sms" }, null, new PageRequest(q.Page, q.PageSize), ct);
    }

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(ListNotificationHistoryQuery q, CancellationToken ct)
    {
        var viewer = Viewer();
        AccessScopePolicy.EnsureCanView(AuditCategory.Notification, viewer, OwnerScope.Of(OwnerType.User, viewer.Id ?? Guid.Empty));
        return await _store.ListNotificationLogAsync(new[] { "InApp", "Email", "Sms" }, viewer.Id, new PageRequest(q.Page, q.PageSize), ct);
    }

    private Viewer Viewer() => new(_user.ActorType, _user.UserId);
}

internal sealed class PartnerDashboardHandlers :
    IQueryHandler<GetSyncDashboardQuery, SyncDashboardDto>,
    IQueryHandler<GetIntegrationStatusDashboardQuery, IntegrationStatusDto>,
    IQueryHandler<GetIntegrationUsageStatisticsQuery, UsageStatisticsDto>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PartnerDashboardHandlers(IAuditReadStore store, ICurrentUser user, TimeProvider clock)
    {
        _store = store;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<SyncDashboardDto>> Handle(GetSyncDashboardQuery q, CancellationToken ct) =>
        await _store.GetSyncDashboardAsync(Partner(), new PageRequest(q.Page, q.PageSize), ct);

    public async Task<Result<IntegrationStatusDto>> Handle(GetIntegrationStatusDashboardQuery q, CancellationToken ct) =>
        await _store.GetIntegrationStatusAsync(Partner(), DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), ct);

    public async Task<Result<UsageStatisticsDto>> Handle(GetIntegrationUsageStatisticsQuery q, CancellationToken ct)
    {
        var window = UsageWindow.Create(q.From, q.To); // end before start => E-TPJPRI-INVALID-FIELD (AL.Usage.INVALID_DATE_RANGE)
        return await _store.GetUsageAsync(Partner(), window, ct);
    }

    private Guid Partner()
    {
        var viewer = new Viewer(_user.ActorType, _user.UserId);
        AccessScopePolicy.EnsureCanView(AuditCategory.ApiCall, viewer, OwnerScope.Of(OwnerType.Partner, viewer.Id ?? Guid.Empty));
        return viewer.Id!.Value;
    }
}

internal sealed class EmployerViewHandlers :
    IQueryHandler<GetEmployerDashboardQuery, EmployerDashboardDto>,
    IQueryHandler<GetJobStatusHistoryQuery, IReadOnlyList<JobStatusHistoryDto>>,
    IQueryHandler<GetCandidateInsightQuery, CandidateInsightDto>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public EmployerViewHandlers(IAuditReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<EmployerDashboardDto>> Handle(GetEmployerDashboardQuery q, CancellationToken ct)
    {
        var viewer = new Viewer(_user.ActorType, _user.UserId);
        if (viewer.Id is not { } employerId)
        {
            return Error.Unauthorized("E-AAFR-UNAUTHORIZED", "Authentication is required.");
        }

        // Only the owner sees own data (3.1.2-07 AC-03): the dashboard is looked up by the caller's own id.
        return await _store.GetEmployerDashboardAsync(employerId, ct)
               ?? new EmployerDashboardDto(false, 0, 0, 0, Array.Empty<DashboardPostingDto>(), null);
    }

    public async Task<Result<IReadOnlyList<JobStatusHistoryDto>>> Handle(GetJobStatusHistoryQuery q, CancellationToken ct)
    {
        var view = await _store.GetJobStatusHistoryAsync(q.JobPostingId, ct);
        var viewer = new Viewer(_user.ActorType, _user.UserId);
        if (view.EmployerId is { } owner)
        {
            AccessScopePolicy.EnsureCanView(AuditCategory.JobStatus, viewer, OwnerScope.Of(OwnerType.Employer, owner));
        }

        return Result.Success(view.Rows);
    }

    public async Task<Result<CandidateInsightDto>> Handle(GetCandidateInsightQuery q, CancellationToken ct)
    {
        var raw = await _store.GetCandidateInsightAsync(q.CandidateId, q.JobPostingId, ct);
        if (raw is null)
        {
            return Error.NotFound(AuditErrorCodes.InsightNotFound, "No insight is available for this candidate and posting.");
        }

        AccessScopePolicy.EnsureCanView(AuditCategory.Insight, new Viewer(_user.ActorType, _user.UserId), OwnerScope.Of(OwnerType.Employer, raw.EmployerId));
        return new CandidateInsightDto(q.CandidateId, q.JobPostingId,
            CandidateInsightPolicy.Availability(raw.Availability, raw.Withheld),
            CandidateInsightPolicy.ExpectedSalary(raw.ExpectedSalary, raw.Withheld),
            CandidateInsightPolicy.Fit(raw.FitScore, raw.Withheld),
            raw.ComputedAtUtc);
    }
}
