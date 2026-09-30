using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetIntegrationStatusDashboardHandler : IQueryHandler<GetIntegrationStatusDashboardQuery, IntegrationStatusDto>
{
    private readonly IAuditReadStore _store;
    private readonly TimeProvider _clock;
    private readonly PartnerDashboardService _partnerDashboardService;

    public GetIntegrationStatusDashboardHandler(IAuditReadStore store, TimeProvider clock, PartnerDashboardService partnerDashboardService)
    {
        _store = store;
        _clock = clock;
        _partnerDashboardService = partnerDashboardService;
    }

    public async Task<Result<IntegrationStatusDto>> Handle(GetIntegrationStatusDashboardQuery q, CancellationToken ct) =>
        await _store.GetIntegrationStatusAsync(_partnerDashboardService.Partner(), DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), ct);
}
