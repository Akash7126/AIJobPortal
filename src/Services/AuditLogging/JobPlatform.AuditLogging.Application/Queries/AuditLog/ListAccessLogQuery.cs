using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record ListAccessLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : AdminRequest(AuditErrorCodes.AccessForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;
