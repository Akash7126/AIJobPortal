using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetSyncDashboardHandler : IQueryHandler<GetSyncDashboardQuery, SyncDashboardDto>
{
    private readonly IAuditReadStore _store;
    private readonly PartnerDashboardService _partnerDashboardService;

    public GetSyncDashboardHandler(IAuditReadStore store, PartnerDashboardService partnerDashboardService)
    {
        _store = store;
        _partnerDashboardService = partnerDashboardService;
    }

    public async Task<Result<SyncDashboardDto>> Handle(GetSyncDashboardQuery q, CancellationToken ct) =>
        await _store.GetSyncDashboardAsync(_partnerDashboardService.Partner(), new PageRequest(q.Page, q.PageSize), ct);
}
