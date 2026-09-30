using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AuditLogging.Application.Queries.AuditLog;

public sealed record GetEmployerDashboardQuery : ActorRequest, IQuery<EmployerDashboardDto>
{
    public GetEmployerDashboardQuery() : base(ActorType.Employer, AuditErrorCodes.EmployerForbidden)
    {
    }
}
