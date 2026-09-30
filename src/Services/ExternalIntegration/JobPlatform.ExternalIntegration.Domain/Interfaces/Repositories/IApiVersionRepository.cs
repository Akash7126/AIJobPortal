namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface IApiVersionRepository
{
    Task<ApiVersion?> GetAsync(string version, CancellationToken ct = default);

    Task<IReadOnlyList<ApiVersion>> ListAsync(CancellationToken ct = default);

    void Add(ApiVersion version);
}
