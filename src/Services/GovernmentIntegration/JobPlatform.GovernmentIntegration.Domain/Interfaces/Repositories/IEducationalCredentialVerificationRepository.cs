namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IEducationalCredentialVerificationRepository
{
    Task<EducationalCredentialVerification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EducationalCredentialVerification?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<EducationalCredentialVerification>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default);

    void Add(EducationalCredentialVerification verification);
}
