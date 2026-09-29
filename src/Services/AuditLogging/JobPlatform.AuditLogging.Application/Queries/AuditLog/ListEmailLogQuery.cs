using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record ListEmailLogQuery(int Page = 1, int PageSize = 20) : AdminRequest(AuditErrorCodes.EmailForbidden), IQuery<PagedResult<NotificationLogDto>>;
