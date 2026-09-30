using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetIntegrationUsageStatisticsQuery(DateOnly From, DateOnly To)
    : ActorRequest(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden), IQuery<UsageStatisticsDto>;
