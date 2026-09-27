using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence;

/// <summary>
/// Idempotent seeding of the reference authority: the initial system settings, the four reference files and the four well-known taxonomies (empty,
/// version 1). With Seed:SampleData=true (development) the skills and occupations taxonomies receive a small sample so consumers have something to
/// validate against. Nothing is published: seeding is not an administrator change.
/// </summary>
internal sealed class AdminSeeder : IDbSeeder<AdminDbContext>
{
    private static readonly Actor SystemActor = new(Guid.Empty, true);

    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;

    public AdminSeeder(IConfiguration configuration, TimeProvider clock)
    {
        _configuration = configuration;
        _clock = clock;
    }

    public async Task SeedAsync(AdminDbContext db, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var existingSettings = (await db.SystemSettings.Select(s => s.Key).ToListAsync(ct)).ToHashSet();
        foreach (var definition in SystemSettingCatalog.Definitions.Where(d => !existingSettings.Contains(d.Key)))
        {
            db.SystemSettings.Add(SystemSetting.Define(definition, now));
        }

        var existingFiles = (await db.ReferenceFiles.Select(f => f.Type).ToListAsync(ct)).ToHashSet();
        foreach (var type in Enum.GetValues<ReferenceFileType>().Where(t => !existingFiles.Contains(t)))
        {
            db.ReferenceFiles.Add(ReferenceFile.Create(type, now));
        }

        var existingTaxonomies = (await db.Taxonomies.Select(t => t.Type).ToListAsync(ct)).ToHashSet();
        var sample = _configuration.GetValue("Seed:SampleData", false);
        foreach (var type in TaxonomyTypes.WellKnown.Where(t => !existingTaxonomies.Contains(t)))
        {
            var taxonomy = PlatformTaxonomy.Create(type, now);
            db.Taxonomies.Add(taxonomy);
            db.TaxonomyVersions.Add(taxonomy.CreateSnapshot(now));
            if (sample && SampleNodes(type) is { Count: > 0 } nodes)
            {
                taxonomy.ApplyChanges(nodes, SystemActor, now);
                taxonomy.ClearDomainEvents();
                db.TaxonomyVersions.Add(taxonomy.CreateSnapshot(now));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<TaxonomyChange> SampleNodes(string type) => type switch
    {
        TaxonomyTypes.Skills => new[]
        {
            Add("csharp", "سي شارب", "C#", synonyms: new[] { "c-sharp", "c sharp" }),
            Add("sql", "لغة الاستعلام", "SQL"),
            Add("communication", "مهارات التواصل", "Communication"),
            Add("accounting", "المحاسبة", "Accounting"),
            Add("arabic-writing", "الكتابة العربية", "Arabic writing")
        },
        TaxonomyTypes.Occupations => new[]
        {
            Add("information-technology", "تكنولوجيا المعلومات", "Information technology"),
            Add("software-development", "تطوير البرمجيات", "Software development", "information-technology"),
            Add("finance", "المالية", "Finance"),
            Add("education", "التعليم", "Education")
        },
        _ => Array.Empty<TaxonomyChange>()
    };

    private static TaxonomyChange Add(string code, string ar, string en, string? parent = null, string[]? synonyms = null) =>
        new(TaxonomyChangeKind.Add, code, new LocalizedText(ar, en), parent, synonyms, null);
}
