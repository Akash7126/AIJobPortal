using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

public sealed record RequestGovernmentVerificationCommand(string RequestingComponent, SubjectType SubjectType, Guid SubjectId, SourceSystem Source,
    AccessPurpose Purpose) : ServiceCommand<Guid>;
