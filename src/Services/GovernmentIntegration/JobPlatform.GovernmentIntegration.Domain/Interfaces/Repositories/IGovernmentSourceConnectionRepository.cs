namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IGovernmentSourceConnectionRepository
{
    Task<GovernmentSourceConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<GovernmentSourceConnection?> GetBySourceAsync(SourceSystem source, CancellationToken ct = default);

    Task<IReadOnlyList<GovernmentSourceConnection>> ListAsync(CancellationToken ct = default);

    void Add(GovernmentSourceConnection connection);
}
