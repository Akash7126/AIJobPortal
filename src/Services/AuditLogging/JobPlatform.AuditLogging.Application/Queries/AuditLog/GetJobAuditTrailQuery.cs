using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetJobAuditTrailQuery(string PlatformJobId, DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20,
    bool IncludeArchived = false)
    : AdminRequest(AuditErrorCodes.AdminForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;
