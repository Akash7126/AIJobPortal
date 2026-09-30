using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record ListSmsMessageLogQuery(int Page = 1, int PageSize = 20) : AdminRequest(AuditErrorCodes.SmsForbidden), IQuery<PagedResult<NotificationLogDto>>;
