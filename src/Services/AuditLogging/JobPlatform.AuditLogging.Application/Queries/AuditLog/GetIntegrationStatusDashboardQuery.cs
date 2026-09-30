using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetIntegrationStatusDashboardQuery : ActorRequest, IQuery<IntegrationStatusDto>
{
    public GetIntegrationStatusDashboardQuery() : base(ActorType.ExternalJobSite, AuditErrorCodes.PartnerForbidden)
    {
    }
}
