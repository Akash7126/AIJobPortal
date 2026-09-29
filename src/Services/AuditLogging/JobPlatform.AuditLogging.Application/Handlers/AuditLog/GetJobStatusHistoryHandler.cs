using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetJobStatusHistoryHandler : IQueryHandler<GetJobStatusHistoryQuery, IReadOnlyList<JobStatusHistoryDto>>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public GetJobStatusHistoryHandler(IAuditReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
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
}
