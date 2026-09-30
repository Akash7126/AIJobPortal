namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IGovernmentVerificationDataRepository
{
    Task<GovernmentVerificationData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<GovernmentVerificationData?> GetLatestForSubjectAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<GovernmentVerificationData>> ListExpiredAsync(DateTime now, int take, CancellationToken ct = default);

    void Add(GovernmentVerificationData data);
}
