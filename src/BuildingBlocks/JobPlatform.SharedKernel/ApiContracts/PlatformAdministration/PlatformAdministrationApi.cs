namespace JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;

/// <summary>
/// Synchronous contract of BC-08 Platform Administration (reference-data authority; routes under /internal/v1).
/// Consumer here: BC-10 (skills taxonomy for skill standardisation).
/// </summary>
public interface IPlatformAdministrationApi
{
    /// <summary>GET /internal/v1/taxonomies/{type}?version= (type "skills"); the ETag is the version. Null = 404.</summary>
    Task<TaxonomyDto?> GetTaxonomyAsync(string type, string? version, CancellationToken ct = default);
}

public sealed record TaxonomyDto(string Type, string Version, IReadOnlyList<TaxonomyEntryDto> Entries);

public sealed record TaxonomyEntryDto(string Code, string LabelEn, string LabelAr, IReadOnlyList<string> Synonyms, bool IsActive, string? ParentCode = null);
