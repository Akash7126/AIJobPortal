using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record ListSubmissionLogQuery(DateTime? From, DateTime? To, string? Outcome, int Page = 1, int PageSize = 20, bool IncludeArchived = false)
    : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden), IQuery<PagedResult<AuditEntryDto>>, IFilteredListQuery;
