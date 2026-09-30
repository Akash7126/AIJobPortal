namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface ISoftwareInterfaceRepository
{
    Task<SoftwareInterfaceConnection?> GetByKeyAsync(SoftwareInterfaceCategory category, string name, CancellationToken ct = default);

    Task<IReadOnlyList<SoftwareInterfaceConnection>> ListAsync(CancellationToken ct = default);

    void Add(SoftwareInterfaceConnection connection);
}
