namespace JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

public sealed record RequestEducationalCredentialVerificationCommand(string RequestingComponent, Guid SubjectId, string Institution, string CredentialName,
    int Year) : ServiceCommand<Guid>;
