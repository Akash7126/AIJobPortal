using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

public sealed record RequestIdentityVerificationCommand(string RequestingComponent, SubjectType SubjectType, Guid SubjectId,
    string NationalIdReference, string FullName, DateOnly DateOfBirth) : ServiceCommand<Guid>;
