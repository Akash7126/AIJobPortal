using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application.Queries.EmployerVerifications;

public sealed record GetEmployerVerificationQuery(Guid EmployerVerificationId) : IQuery<EmployerVerificationView>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer, ActorType.Administrator };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}
