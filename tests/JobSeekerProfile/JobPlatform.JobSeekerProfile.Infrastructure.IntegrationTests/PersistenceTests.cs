using System.Text.Json.Nodes;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Application.Events;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static SqliteTestDatabase<JobSeekerProfileDbContext> New() =>
        new(o => new JobSeekerProfileDbContext(o), new JobSeekerProfileEventMapper());

    public static Profile NewProfile(Guid ownerId) => Profile.Create(Guid.NewGuid(), ownerId, new FullName("Layla Haddad"), Email.Create("layla@example.org"),
        MobileNumber.Create("+970590000001"), Gender.Female, true, ownerId, T0);
}

public class ProfileRepositoryTests
{
    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    public async Task RoundTrips_Level1AndSections()
    {
        await using var database = Db.New();
        var ownerId = Guid.NewGuid();
        var profile = Db.NewProfile(ownerId);
        profile.UpdateEducation(new Actor(ownerId), new[] { ("BSc", "Birzeit", (DateTime?)null, (DateTime?)null) }, ownerId, Db.T0);
        profile.UpdateSkills(new Actor(ownerId), new[] { ("C#", SkillKind.Primary, SkillClass.Hard) }, ownerId, Db.T0);
        await using (var write = database.NewContext())
        {
            write.Profiles.Add(profile);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new ProfileRepository(read).GetByOwnerAsync(ownerId);
        loaded!.FullName.Value.Should().Be("Layla Haddad");
        loaded.Education.Should().ContainSingle().Which.Degree.Should().Be("BSc");
        loaded.Skills.Should().ContainSingle().Which.Name.Should().Be("C#");
        loaded.CompletionPercent.Should().Be(CompletionWeights.Level1 + CompletionWeights.Education + CompletionWeights.Skills);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    public async Task SameOwnerTwice_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        var ownerId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.Profiles.Add(Db.NewProfile(ownerId));
        await db.SaveChangesAsync();
        db.Profiles.Add(Db.NewProfile(ownerId));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    [Trait("AC", "AC-03")]
    public async Task ConcurrentEdit_ThrowsConcurrencyConflict_WhenRowVersionIsStale()
    {
        await using var database = Db.New();
        var ownerId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.Profiles.Add(Db.NewProfile(ownerId));
            await write.SaveChangesAsync();
        }

        await using var first = database.NewContext();
        var a = await new ProfileRepository(first).GetByOwnerAsync(ownerId);
        await using var second = database.NewContext();
        var b = await new ProfileRepository(second).GetByOwnerAsync(ownerId);

        a!.UpdateLevel1(new Actor(ownerId), a.FullName, a.Email, a.MobileNumber, Gender.Male, ownerId, Db.T0);
        await first.SaveChangesAsync();
        b!.UpdateLevel1(new Actor(ownerId), b.FullName, b.Email, b.MobileNumber, Gender.Other, ownerId, Db.T0);
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }
}

public class OutboxTests
{
    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    [Trait("AC", "AC-04")]
    public async Task ProfileCreated_WritesTheOutboxRowInTheSameSaveAsTheAggregate()
    {
        await using var database = Db.New();
        var ownerId = Guid.NewGuid();
        var profile = Db.NewProfile(ownerId);
        await using (var write = database.NewContext())
        {
            write.Profiles.Add(profile);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var row = await read.Set<OutboxMessage>().SingleAsync();
        (row.Type, row.Exchange, row.RoutingKey, row.Status).Should().Be(("ProfileCreated", "jobplatform.job-seeker-profile.events", "profile.created.v1", OutboxStatus.Pending));
        var payload = JsonNode.Parse(row.Payload)!;
        payload["profileId"]!.GetValue<Guid>().Should().Be(profile.Id);
        payload["ownerAccountId"]!.GetValue<Guid>().Should().Be(ownerId);
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        await using var database = Db.New();
        await using (var db = database.NewContext())
        {
            await db.BeginTransactionAsync();
            db.Profiles.Add(Db.NewProfile(Guid.NewGuid()));
            await db.SaveChangesAsync();
            await db.RollbackTransactionAsync();
        }

        await using var read = database.NewContext();
        (await read.Profiles.CountAsync()).Should().Be(0);
        (await read.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }
}
