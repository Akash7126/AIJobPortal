using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

public interface IEducationalInstitutionClient
{
    Task<EducationalCheckResult> CheckAsync(Credential credential, CancellationToken ct);
}
