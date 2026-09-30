using JobPlatform.PlatformAdministration.Application.DTOs.Entities;
using JobPlatform.PlatformAdministration.Application.DTOs.Offerings;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Settings;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.PlatformAdministration.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IAdminReadStore
{
    Task<IReadOnlyList<SettingView>> ListSettingsAsync(CancellationToken ct = default);

    Task<SettingView?> GetSettingAsync(string key, CancellationToken ct = default);

    Task<ReferenceFileView?> GetReferenceFileAsync(string type, CancellationToken ct = default);

    /// <summary>Current version of a taxonomy, or null when it does not exist.</summary>
    Task<int?> GetTaxonomyCurrentVersionAsync(string type, CancellationToken ct = default);

    /// <summary>The taxonomy at a version (null = current). Null when the taxonomy or the version does not exist.</summary>
    Task<TaxonomyView?> GetTaxonomyAsync(string type, int? version, CancellationToken ct = default);

    Task<PagedResult<JobOfferingListItem>> ListJobOfferingsAsync(string? status, PageRequest page, CancellationToken ct = default);

    Task<EntityRecordView?> GetEntityRecordAsync(Guid id, CancellationToken ct = default);
}
