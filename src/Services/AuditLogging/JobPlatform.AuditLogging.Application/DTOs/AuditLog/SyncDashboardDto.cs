using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.DTOs.AuditLog;

public sealed record SyncDashboardDto(int Pending, int Synced, int Failed, int Archived, PagedResult<SyncJobStatusDto> Jobs);
