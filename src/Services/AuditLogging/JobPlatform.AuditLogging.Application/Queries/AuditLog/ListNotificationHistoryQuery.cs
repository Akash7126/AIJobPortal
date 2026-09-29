using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

/// <summary>The caller's own notification history, newest first (3.6.2-04).</summary>
public sealed record ListNotificationHistoryQuery(int Page = 1, int PageSize = 20)
    : AuthenticatedRequest(AuditErrorCodes.NotificationForbidden), IQuery<PagedResult<NotificationLogDto>>;
