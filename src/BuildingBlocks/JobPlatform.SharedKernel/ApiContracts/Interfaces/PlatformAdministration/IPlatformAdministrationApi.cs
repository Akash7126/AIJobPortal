using JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;

namespace JobPlatform.SharedKernel.ApiContracts.Interfaces.PlatformAdministration;

/// <summary>
/// Synchronous contract of BC-08 Platform Administration (reference-data authority; routes under /internal/v1).
/// Consumer here: BC-10 (skills taxonomy for skill standardisation).
/// </summary>
public interface IPlatformAdministrationApi
{
    /// <summary>GET /internal/v1/taxonomies/{type}?version= (type "skills"); the ETag is the version. Null = 404.</summary>
    Task<TaxonomyDto?> GetTaxonomyAsync(string type, string? version, CancellationToken ct = default);
}
