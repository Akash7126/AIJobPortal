using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetIntegrationUsageStatisticsHandler : IQueryHandler<GetIntegrationUsageStatisticsQuery, UsageStatisticsDto>
{
    private readonly IAuditReadStore _store;
    private readonly PartnerDashboardService _partnerDashboardService;

    public GetIntegrationUsageStatisticsHandler(IAuditReadStore store, PartnerDashboardService partnerDashboardService)
    {
        _store = store;
        _partnerDashboardService = partnerDashboardService;
    }

    public async Task<Result<UsageStatisticsDto>> Handle(GetIntegrationUsageStatisticsQuery q, CancellationToken ct)
    {
        var window = UsageWindow.Create(q.From, q.To); // end before start => E-TPJPRI-INVALID-FIELD (AL.Usage.INVALID_DATE_RANGE)
        return await _store.GetUsageAsync(_partnerDashboardService.Partner(), window, ct);
    }
}
