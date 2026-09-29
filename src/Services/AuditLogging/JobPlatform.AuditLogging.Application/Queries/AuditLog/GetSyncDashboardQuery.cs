using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetSyncDashboardQuery(int Page = 1, int PageSize = 20) : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden),
    IQuery<SyncDashboardDto>;
