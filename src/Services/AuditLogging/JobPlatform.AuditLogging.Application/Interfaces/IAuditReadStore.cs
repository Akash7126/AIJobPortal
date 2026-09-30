using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IAuditReadStore
{
    Task<PagedResult<AuditEntryDto>> ListEntriesAsync(EntryFilter filter, PageRequest page, CancellationToken ct = default);

    Task<SyncDashboardDto> GetSyncDashboardAsync(Guid partnerId, PageRequest page, CancellationToken ct = default);

    Task<IntegrationStatusDto> GetIntegrationStatusAsync(Guid partnerId, DateOnly today, CancellationToken ct = default);

    Task<UsageStatisticsDto> GetUsageAsync(Guid partnerId, UsageWindow window, CancellationToken ct = default);

    Task<JobHistoryOwnerView> GetJobStatusHistoryAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<PagedResult<NotificationLogDto>> ListNotificationLogAsync(string[] channels, Guid? recipientId, PageRequest page, CancellationToken ct = default);

    Task<EmployerDashboardDto?> GetEmployerDashboardAsync(Guid employerId, CancellationToken ct = default);

    Task<CandidateInsightRaw?> GetCandidateInsightAsync(Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default);
}
