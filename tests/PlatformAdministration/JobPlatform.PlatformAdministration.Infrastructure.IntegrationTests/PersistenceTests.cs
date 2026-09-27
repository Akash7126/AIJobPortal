using JobPlatform.PlatformAdministration.Application.Events;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Actor Admin = new(Guid.NewGuid(), true);

    public static SqliteTestDatabase<AdminDbContext> New() => new(o => new AdminDbContext(o), new PlatformAdministrationEventMapper());

    public static TaxonomyChange Add(string code, string? parent = null) =>
        new(TaxonomyChangeKind.Add, code, new LocalizedText(code + "-ar", code + "-en"), parent, new[] { "syn-" + code }, null);
}

public class RepositoryTests
{
    [Fact]
    public async Task EntityRecord_RoundTripsCoreJsonAndKey()
    {
        await using var database = Db.New();
        var core = new Dictionary<string, string> { ["companyName"] = "Acme", ["companyId"] = "CO-1" };
        await using (var write = database.NewContext())
        {
            write.PlatformEntityRecords.Add(PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.Employer, core, Db.Admin, false, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new PlatformEntityRecordRepository(read).GetByIdAsync((await read.PlatformEntityRecords.SingleAsync()).Id);
        loaded!.Core.Fields.Should().BeEquivalentTo(core);
        loaded.EntityType.Should().Be(PlatformEntityType.Employer);
        loaded.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        (await new PlatformEntityRecordRepository(read).ExistsByKeyAsync(PlatformEntityType.Employer, "co-1")).Should().BeTrue();
        (await new PlatformEntityRecordRepository(read).ExistsByKeyAsync(PlatformEntityType.JobSeeker, "co-1")).Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-03")]
    public async Task EntityRecord_SameTypeAndKey_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        var core = new Dictionary<string, string> { ["title"] = "T", ["postingReference"] = "R-1" };
        await using var db = database.NewContext();
        db.PlatformEntityRecords.Add(PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.JobOffering, core, Db.Admin, false, Db.T0));
        await db.SaveChangesAsync();
        db.PlatformEntityRecords.Add(PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.JobOffering, core, Db.Admin, false, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-03")]
    public async Task SystemSetting_RoundTripsBounds_AndEveryChangeAppendsAHistoryRow()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            write.SystemSettings.Add(SystemSetting.Define(SystemSettingCatalog.Find("platform.defaultLanguage")!, Db.T0));
            write.SystemSettings.Add(SystemSetting.Define(SystemSettingCatalog.Find("upload.maxSizeMb")!, Db.T0));
            await write.SaveChangesAsync();
        }

        for (var i = 0; i < 2; i++)
        {
            await using var change = database.NewContext();
            var setting = await new SystemSettingRepository(change).GetByKeyAsync("upload.maxSizeMb");
            setting!.Change((10 + i).ToString(), Db.Admin, Db.T0.AddMinutes(i));
            await change.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var language = await read.SystemSettings.SingleAsync(s => s.Key == "platform.defaultLanguage");
        language.Bounds.AllowedValues.Should().Equal("ar", "en");
        var upload = await read.SystemSettings.SingleAsync(s => s.Key == "upload.maxSizeMb");
        (upload.Value, upload.SettingVersion, upload.Bounds.Min, upload.Bounds.Max).Should().Be(("11", 3, 1m, 50m));
        (await read.SystemSettingHistory.OrderBy(h => h.SettingVersion).Select(h => h.NewValue).ToListAsync()).Should().Equal("10", "11");
    }

    [Fact]
    public async Task ReferenceFile_RoundTripsEntries_AndCodesAreUniquePerFile()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var file = ReferenceFile.Create(ReferenceFileType.Skills, Db.T0);
            file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Add, null, "a", new LocalizedText("أ", "A"), null) }, false, Array.Empty<string>(), Db.Admin, Db.T0);
            write.ReferenceFiles.Add(file);
            write.ReferenceFiles.Add(ReferenceFile.Create(ReferenceFileType.Jobs, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new ReferenceFileRepository(read).GetByTypeAsync(ReferenceFileType.Skills);
        var entry = loaded!.Entries.Single();
        (entry.Code, entry.Name.Ar, entry.Name.En, entry.IsActive, loaded.FileVersion).Should().Be(("a", "أ", "A", true, 2));

        var duplicateFile = ReferenceFile.Create(ReferenceFileType.Skills, Db.T0);
        read.ReferenceFiles.Add(duplicateFile);
        var act = () => read.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>("one reference file per type");
    }

    [Fact]
    public async Task Taxonomy_RoundTripsNodesSynonymsAndSnapshots_AndCodesAreUniquePerTaxonomy()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var taxonomy = PlatformTaxonomy.Create("skills", Db.T0);
            write.Taxonomies.Add(taxonomy);
            write.TaxonomyVersions.Add(taxonomy.CreateSnapshot(Db.T0));
            taxonomy.ApplyChanges(new[] { Db.Add("it"), Db.Add("dev", "it") }, Db.Admin, Db.T0);
            write.TaxonomyVersions.Add(taxonomy.CreateSnapshot(Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new PlatformTaxonomyRepository(read).GetByTypeAsync("skills");
        loaded!.TaxonomyVersion.Should().Be(2);
        var dev = loaded.Nodes.Single(n => n.Code == "dev");
        (dev.ParentCode, dev.Name.En, dev.Synonyms).Should().Be(("it", "dev-en", dev.Synonyms));
        dev.Synonyms.Should().Equal("syn-dev");
        (await read.TaxonomyVersions.OrderBy(s => s.TaxonomyVersion).ToListAsync()).Select(s => (s.TaxonomyVersion, s.Nodes.Count)).Should().Equal((1, 0), (2, 2));

        read.TaxonomyVersions.Add(loaded.CreateSnapshot(Db.T0));
        var act = () => read.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>("a snapshot is immutable per (taxonomy, version)");
    }

    [Fact]
    public async Task JobOffering_RoundTripsModerationState_AndReadStorePagesByStatus()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            for (var i = 0; i < 3; i++)
            {
                var offering = JobOffering.Register(Guid.NewGuid(), Guid.NewGuid(), $"Job {i}", Db.T0.AddMinutes(i));
                if (i == 0)
                {
                    offering.Suspend(Db.Admin, "Misleading", Db.T0);
                }

                write.JobOfferings.Add(offering);
            }

            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new AdminReadStore(read);
        var inactive = await store.ListJobOfferingsAsync("inactive", new(1, 10));
        inactive.Items.Should().ContainSingle().Which.Should().Match<JobPlatform.PlatformAdministration.Application.JobOfferingListItem>(
            i => i.Title == "Job 0" && i.Moderation == "Suspended" && i.Reason == "Misleading");
        (await store.ListJobOfferingsAsync("active", new(1, 1))).Should().Match<JobPlatform.SharedKernel.Application.Paging.PagedResult<JobPlatform.PlatformAdministration.Application.JobOfferingListItem>>(
            p => p.TotalCount == 2 && p.Items.Count == 1 && p.TotalPages == 2);
        (await store.ListJobOfferingsAsync(null, new(1, 10))).TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task ReadStore_ServesCurrentAndHistoricalTaxonomyVersions()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var taxonomy = PlatformTaxonomy.Create("skills", Db.T0);
            write.Taxonomies.Add(taxonomy);
            write.TaxonomyVersions.Add(taxonomy.CreateSnapshot(Db.T0));
            taxonomy.ApplyChanges(new[] { Db.Add("a") }, Db.Admin, Db.T0);
            write.TaxonomyVersions.Add(taxonomy.CreateSnapshot(Db.T0));
            taxonomy.ApplyChanges(new[] { Db.Add("b") }, Db.Admin, Db.T0);
            write.TaxonomyVersions.Add(taxonomy.CreateSnapshot(Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new AdminReadStore(read);

        (await store.GetTaxonomyCurrentVersionAsync("skills")).Should().Be(3);
        (await store.GetTaxonomyAsync("skills", null))!.Nodes.Select(n => n.Code).Should().Equal("a", "b");
        (await store.GetTaxonomyAsync("skills", 2))!.Nodes.Select(n => n.Code).Should().Equal("a");
        (await store.GetTaxonomyAsync("skills", 1))!.Nodes.Should().BeEmpty();
        (await store.GetTaxonomyAsync("skills", 9)).Should().BeNull();
        (await store.GetTaxonomyAsync("nope", null)).Should().BeNull();
        (await store.GetTaxonomyCurrentVersionAsync("nope")).Should().BeNull();
    }
}
