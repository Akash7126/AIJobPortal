using JobPlatform.CandidateSourcing.Application;
using JobPlatform.CandidateSourcing.Application.Events;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using JobPlatform.CandidateSourcing.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static SqliteTestDatabase<CandidateSourcingDbContext> New() => new(o => new CandidateSourcingDbContext(o), new CandidateSourcingEventMapper());
}

public class RepositoryTests
{
    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-02")]
    public async Task TalentPoolEntry_DuplicateActiveTriple_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var posting = Guid.NewGuid();
        await using var db = database.NewContext();
        db.TalentPoolEntries.Add(TalentPoolEntry.Create(employer, candidate, posting, null, Db.T0, employer, true));
        await db.SaveChangesAsync();
        db.TalentPoolEntries.Add(TalentPoolEntry.Create(employer, candidate, posting, null, Db.T0, employer, true));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task TalentPoolEntry_AfterRemove_CanBeReAdded()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var posting = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var entry = TalentPoolEntry.Create(employer, candidate, posting, null, Db.T0, employer, true);
            write.TalentPoolEntries.Add(entry);
            await write.SaveChangesAsync();
            entry.Remove(employer);
            await write.SaveChangesAsync();
        }

        await using var db = database.NewContext();
        db.TalentPoolEntries.Add(TalentPoolEntry.Create(employer, candidate, posting, "second time", Db.T0, employer, true));
        await db.SaveChangesAsync();

        (await db.TalentPoolEntries.CountAsync()).Should().Be(2);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    public async Task TalentPoolEntry_Created_WritesOutboxRowAtomically()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.TalentPoolEntries.Add(TalentPoolEntry.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Db.T0, Guid.NewGuid(), true));

        await db.SaveChangesAsync();

        var outbox = await db.Set<JobPlatform.BuildingBlocks.Infrastructure.Persistence.OutboxMessage>().SingleAsync();
        outbox.Type.Should().Be("TalentPoolEntryCreated");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-04")]
    public async Task QualificationThreshold_OneAggregatePerEmployerAndPosting()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        var posting = Guid.NewGuid();
        await using var db = database.NewContext();
        db.QualificationThresholds.Add(QualificationThreshold.Open(employer, posting, Db.T0));
        await db.SaveChangesAsync();
        db.QualificationThresholds.Add(QualificationThreshold.Open(employer, posting, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task CandidateProjection_RoundTripsSkillsAndVisibility()
    {
        await using var database = Db.New();
        var profileId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var projection = CandidateProjection.Create(profileId, Guid.NewGuid());
            projection.ApplyCandidateView(CandidateVisibility.Public, true, false, new[] { "sql", "csharp" }, "Bachelor", 4m, "PS-RAM", 2000m, 3000m,
                "Immediate", 1, Db.T0);
            write.CandidateProjections.Add(projection);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new CandidateProjectionRepository(read).GetAsync(profileId);
        loaded!.Skills.Should().Equal("sql", "csharp");
        loaded.Visibility.Should().Be(CandidateVisibility.Public);
        loaded.EmployerVisibilityOptIn.Should().BeTrue();
    }

    [Fact]
    public async Task CandidateInsight_RoundTripsFitBreakdown()
    {
        await using var database = Db.New();
        var insight = CandidateInsight.Compute(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { "availability" }, "Immediate", null,
            null, 82m, new (string, decimal, bool)[] { ("SkillOverlap", 90m, true), ("Location", 20m, true) }, Db.T0);
        await using (var write = database.NewContext())
        {
            write.CandidateInsights.Add(insight);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await read.CandidateInsights.SingleAsync();
        loaded.Fit.OverallScore.Should().Be(82m);
        loaded.Fit.Strengths.Should().Contain("SkillOverlap");
        loaded.Fit.Gaps.Should().Contain("Location");
        loaded.Availability.Should().Be("Immediate");
    }

    [Fact]
    public async Task VerifiedEmployer_RoundTrips()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.VerifiedEmployers.Add(VerifiedEmployer.Create(employer, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new VerifiedEmployerRepository(read).IsVerifiedAsync(employer)).Should().BeTrue();
        (await new VerifiedEmployerRepository(read).IsVerifiedAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    [Trait("AC", "AC-01")]
    public async Task ReadStore_SearchCandidates_FiltersBySkillsAndExcludesPrivateAndDeactivated()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var visible = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());
            visible.ApplyCandidateView(CandidateVisibility.Public, false, false, new[] { "sql" }, null, null, null, null, null, null, 1, Db.T0);
            var privateOne = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());
            privateOne.ApplyCandidateView(CandidateVisibility.Private, false, false, new[] { "sql" }, null, null, null, null, null, null, 1, Db.T0);
            var deactivated = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());
            deactivated.ApplyCandidateView(CandidateVisibility.Public, false, true, new[] { "sql" }, null, null, null, null, null, null, 1, Db.T0);
            var noMatch = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());
            noMatch.ApplyCandidateView(CandidateVisibility.Public, false, false, new[] { "java" }, null, null, null, null, null, null, 1, Db.T0);
            write.CandidateProjections.AddRange(visible, privateOne, deactivated, noMatch);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new CandidateSourcingReadStore(read);
        var result = await store.SearchCandidatesAsync(new CandidateSearchCriteria(new[] { "sql" }, null, null, null, null, null, null, null), new PageRequest(1, 10));

        result.TotalCount.Should().Be(1);
    }
}
