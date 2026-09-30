namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IIdentityVerificationRepository
{
    Task<IdentityVerificationData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IdentityVerificationData?> GetLatestForSubjectAsync(Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<IdentityVerificationData>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default);

    void Add(IdentityVerificationData verification);
}
