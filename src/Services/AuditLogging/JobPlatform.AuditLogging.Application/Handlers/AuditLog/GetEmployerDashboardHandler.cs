using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetEmployerDashboardHandler : IQueryHandler<GetEmployerDashboardQuery, EmployerDashboardDto>
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public GetEmployerDashboardHandler(IAuditReadStore store, ICurrentUser user)
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
}
