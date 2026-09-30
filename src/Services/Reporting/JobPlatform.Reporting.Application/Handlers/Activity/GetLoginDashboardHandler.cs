using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Queries.Activity;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Reporting.Application.Handlers.Activity;

internal sealed class GetLoginDashboardHandler : IQueryHandler<GetLoginDashboardQuery, LoginDashboardDto>
{
    private readonly IReportAccessGuard _guard;
    private readonly ISessionSource _sessions;

    public GetLoginDashboardHandler(IReportAccessGuard guard, ISessionSource sessions)
    {
        _guard = guard;
        _sessions = sessions;
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
}
