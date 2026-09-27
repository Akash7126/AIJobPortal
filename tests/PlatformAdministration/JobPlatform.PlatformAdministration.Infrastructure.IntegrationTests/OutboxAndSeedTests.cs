using System.Text.Json.Nodes;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobPlatform.PlatformAdministration.Infrastructure.IntegrationTests;

public class OutboxTests
{
    [Fact]
    [Trait("Story", "US-3.1.4-09")]
    [Trait("AC", "AC-04")]
    public async Task Suspension_WritesTheOutboxRowInTheSameSaveAsTheAggregate()
    {
        await using var database = Db.New();
        var offering = JobOffering.Register(Guid.NewGuid(), Guid.NewGuid(), "Engineer", Db.T0);
        await using (var write = database.NewContext())
        {
            write.JobOfferings.Add(offering);
            await write.SaveChangesAsync();
            offering.Suspend(Db.Admin, "Policy breach", Db.T0);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var row = await read.Set<OutboxMessage>().SingleAsync();
        (row.Type, row.Exchange, row.RoutingKey, row.Status).Should().Be(("JobOfferingSuspended", "jobplatform.platform-administration.events", "job-offering.suspended.v1", OutboxStatus.Pending));
        row.AggregateId.Should().Be(offering.Id.ToString());
        var payload = JsonNode.Parse(row.Payload)!;
        payload["jobOfferingId"]!.GetValue<Guid>().Should().Be(offering.Id);
        payload["jobPostingId"]!.GetValue<Guid>().Should().Be(offering.Id);
        payload["reason"]!.GetValue<string>().Should().Be("Policy breach");
        payload["actorId"]!.GetValue<Guid>().Should().Be(Db.Admin.Id);
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        await using var database = Db.New();
        await using (var db = database.NewContext())
        {
            await db.BeginTransactionAsync();
            var taxonomy = PlatformTaxonomy.Create("skills", Db.T0);
            taxonomy.ApplyChanges(new[] { Db.Add("a") }, Db.Admin, Db.T0);
            db.Taxonomies.Add(taxonomy);
            await db.SaveChangesAsync();
            await db.RollbackTransactionAsync();
        }

        await using var read = database.NewContext();
        (await read.Taxonomies.CountAsync()).Should().Be(0);
        (await read.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TaxonomyUpdate_PublishesVersionsAndChangedCodes_AndSettingEventNeverCarriesTheValue()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var taxonomy = PlatformTaxonomy.Create("skills", Db.T0);
            taxonomy.ApplyChanges(new[] { Db.Add("a"), Db.Add("b") }, Db.Admin, Db.T0);
            var setting = SystemSetting.Define(SystemSettingCatalog.Find("platform.supportEmail")!, Db.T0);
            setting.Change("secret@example.org", Db.Admin, Db.T0);
            write.Taxonomies.Add(taxonomy);
            write.SystemSettings.Add(setting);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var rows = await read.Set<OutboxMessage>().ToListAsync();
        var taxonomyEvent = JsonNode.Parse(rows.Single(r => r.Type == "PlatformTaxonomyUpdated").Payload)!;
        (taxonomyEvent["taxonomyType"]!.GetValue<string>(), taxonomyEvent["fromVersion"]!.GetValue<int>(), taxonomyEvent["toVersion"]!.GetValue<int>()).Should().Be(("skills", 1, 2));
        taxonomyEvent["changedCodes"]!.AsArray().Select(n => n!.GetValue<string>()).Should().BeEquivalentTo("a", "b");
        rows.Single(r => r.Type == "SystemSettingChanged").Payload.Should().NotContain("secret@example.org");
    }
}

public class SeederTests
{
    private static async Task SeedAsync(AdminDbContext db, bool sample)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:SampleData"] = sample.ToString() }).Build();
        await new AdminSeeder(configuration, TimeProvider.System).SeedAsync(db, default);
    }

    [Fact]
    public async Task Seed_CreatesSettingsFilesAndEmptyTaxonomies_AndIsIdempotent()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();

        await SeedAsync(db, false);
        await SeedAsync(db, false);

        (await db.SystemSettings.CountAsync()).Should().Be(SystemSettingCatalog.Definitions.Count);
        (await db.ReferenceFiles.CountAsync()).Should().Be(4);
        (await db.Taxonomies.Select(t => t.Type).ToListAsync()).Should().BeEquivalentTo(TaxonomyTypes.WellKnown);
        (await db.Taxonomies.AllAsync(t => t.TaxonomyVersion == 1)).Should().BeTrue();
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0, "seeding is not an administrator change and publishes nothing");
    }

    [Fact]
    public async Task Seed_WithSampleData_FillsSkillsAndOccupationsWithSnapshots()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();

        await SeedAsync(db, true);

        var skills = await db.Taxonomies.Include(t => t.Nodes).SingleAsync(t => t.Type == "skills");
        (skills.TaxonomyVersion, skills.Nodes.Count).Should().Be((2, 5));
        (await db.TaxonomyVersions.CountAsync(s => s.Type == "skills")).Should().Be(2);
        (await db.Taxonomies.Include(t => t.Nodes).SingleAsync(t => t.Type == "occupations")).Nodes.Should().Contain(n => n.Code == "software-development" && n.ParentCode == "information-technology");
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }
}

public class LaterSaveWinsTests
{
    private sealed record Ping : IRequest<int>, ILaterSaveWinsCommand;

    private sealed record Plain : IRequest<int>;

    private static LaterSaveWinsBehavior<TRequest, int> Behavior<TRequest>(AdminDbContext db) where TRequest : IRequest<int> =>
        new(db, new DomainEventBuffer(), NullLogger<LaterSaveWinsBehavior<TRequest, int>>.Instance);

    private static Result<int> Conflict() => Error.Conflict("E-CONCURRENCY-CONFLICT", "lost");

    [Fact]
    [Trait("Story", "US-3.1.4-08")]
    [Trait("AC", "AC-03")]
    public async Task LostRace_IsRetriedOnFreshStateUntilItSucceeds()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var stale = PlatformTaxonomy.Create("skills", Db.T0);
        db.Taxonomies.Add(stale);
        var attempts = 0;

        var result = await Behavior<Ping>(db).Handle(new Ping(), () =>
        {
            attempts++;
            db.ChangeTracker.Entries().Should().HaveCount(attempts == 1 ? 1 : 0, "the stale tracked state is discarded before a retry");
            return Task.FromResult(attempts < 3 ? Conflict() : Result.Success(42));
        }, default);

        (result.IsSuccess, attempts).Should().Be((true, 3));
    }

    [Fact]
    public async Task PersistentConflict_GivesUpAfterTheMaximumAttemptsAndReportsIt()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var attempts = 0;

        var result = await Behavior<Ping>(db).Handle(new Ping(), () =>
        {
            attempts++;
            return Task.FromResult(Conflict());
        }, default);

        (result.Error!.Code, attempts).Should().Be(("E-CONCURRENCY-CONFLICT", LaterSaveWinsBehavior<Ping, int>.MaxAttempts));
    }

    [Fact]
    public async Task OtherErrorsAndOtherRequests_PassThroughUntouched()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var attempts = 0;

        var failure = await Behavior<Ping>(db).Handle(new Ping(), () => { attempts++; return Task.FromResult<Result<int>>(Error.NotFound("x", "y")); }, default);
        var plain = await Behavior<Plain>(db).Handle(new Plain(), () => { attempts++; return Task.FromResult(Conflict()); }, default);

        (failure.Error!.Code, plain.Error!.Code, attempts).Should().Be(("x", "E-CONCURRENCY-CONFLICT", 2));
    }
}
