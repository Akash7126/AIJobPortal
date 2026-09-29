using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Application.DTOs.Entities;
using JobPlatform.PlatformAdministration.Application.DTOs.Offerings;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Settings;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

/// <summary>Read side: AsNoTracking projections, never aggregates (foundation section 3.5).</summary>
internal sealed class AdminReadStore(AdminDbContext db) : IAdminReadStore
{
    public async Task<IReadOnlyList<SettingView>> ListSettingsAsync(CancellationToken ct = default) =>
        (await db.SystemSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(ct)).Select(ToView).ToList();

    public async Task<SettingView?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        var setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting is null ? null : ToView(setting);
    }

    public async Task<ReferenceFileView?> GetReferenceFileAsync(string type, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Domain.Reference.ReferenceFileType>(type, true, out var fileType))
        {
            return null;
        }

        var file = await db.ReferenceFiles.AsNoTracking().Include(f => f.Entries).FirstOrDefaultAsync(f => f.Type == fileType, ct);
        if (file is null)
        {
            return null;
        }

        return new ReferenceFileView(file.Id, file.Type.ToString().ToLowerInvariant(), file.FileVersion,
            file.Entries.OrderBy(e => e.Code, StringComparer.Ordinal)
                .Select(e => new ReferenceEntryView(e.Id, e.Code, new LocalizedNameView(e.Name.Ar, e.Name.En), e.IsActive)).ToList());
    }

    public async Task<int?> GetTaxonomyCurrentVersionAsync(string type, CancellationToken ct = default) =>
        await db.Taxonomies.AsNoTracking().Where(t => t.Type == type).Select(t => (int?)t.TaxonomyVersion).FirstOrDefaultAsync(ct);

    public async Task<TaxonomyView?> GetTaxonomyAsync(string type, int? version, CancellationToken ct = default)
    {
        var taxonomy = await db.Taxonomies.AsNoTracking().Include(t => t.Nodes).FirstOrDefaultAsync(t => t.Type == type, ct);
        if (taxonomy is null)
        {
            return null;
        }

        if (version is null || version == taxonomy.TaxonomyVersion)
        {
            return new TaxonomyView(taxonomy.Id, taxonomy.Type, taxonomy.TaxonomyVersion,
                taxonomy.Nodes.OrderBy(n => n.Code, StringComparer.Ordinal)
                    .Select(n => new TaxonomyNodeView(n.Code, new LocalizedNameView(n.Name.Ar, n.Name.En), n.ParentCode, n.Synonyms, n.IsActive)).ToList());
        }

        var snapshot = await db.TaxonomyVersions.AsNoTracking().FirstOrDefaultAsync(s => s.TaxonomyId == taxonomy.Id && s.TaxonomyVersion == version, ct);
        return snapshot is null
            ? null
            : new TaxonomyView(taxonomy.Id, taxonomy.Type, snapshot.TaxonomyVersion,
                snapshot.Nodes.Select(n => new TaxonomyNodeView(n.Code, new LocalizedNameView(n.NameAr, n.NameEn), n.ParentCode, n.Synonyms, n.IsActive)).ToList());
    }

    public async Task<PagedResult<JobOfferingListItem>> ListJobOfferingsAsync(string? status, PageRequest page, CancellationToken ct = default)
    {
        var query = db.JobOfferings.AsNoTracking().AsQueryable();
        if (status is not null && Enum.TryParse<JobOfferingStatus>(status, true, out var parsed))
        {
            query = query.Where(o => o.Status == parsed);
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(o => o.RegisteredAtUtc).ThenBy(o => o.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        var items = rows.Select(o => new JobOfferingListItem(o.Id, o.EmployerId, o.Title, o.Status.ToString(), o.Moderation?.ToString(), o.SuspendedBy,
            o.SuspendedAtUtc, o.Reason, o.RegisteredAtUtc)).ToList();
        return new PagedResult<JobOfferingListItem>(items, page.Page, page.PageSize, total);
    }

    public async Task<EntityRecordView?> GetEntityRecordAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.PlatformEntityRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return record is null
            ? null
            : new EntityRecordView(record.Id, record.EntityType.ToString(), record.IdentityKey, record.Status.ToString(), record.CreatedBy, record.CreatedAtUtc);
    }

    private static SettingView ToView(Domain.Settings.SystemSetting s) =>
        new(s.Key, s.ValueType.ToString(), s.Value, s.SettingVersion, new SettingBoundsView(s.Bounds.Min, s.Bounds.Max, s.Bounds.AllowedValues, s.Bounds.MaxLength),
            s.UpdatedBy, s.UpdatedAtUtc);
}
