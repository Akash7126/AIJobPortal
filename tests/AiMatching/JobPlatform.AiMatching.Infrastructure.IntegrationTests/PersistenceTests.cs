using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Infrastructure.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Actor Admin = new(Guid.NewGuid(), JobPlatform.SharedKernel.Common.Enums.ActorType.Administrator);

    public static SqliteTestDatabase<AiMatchingDbContext> New() => new(o => new AiMatchingDbContext(o), new AiMatchingEventMapper());
}

public class MatchingConfigurationRepositoryTests
{
    /// <summary>
    /// Regression test: MatchingConfiguration.CreateDefault seeds one ConfigurationHistoryEntry whose Weights aliased the same
    /// CriterionWeights.Default singleton as the aggregate's own Weights. EF Core's change tracker identifies owned-type instances by
    /// CLR reference, so two owned slots sharing one instance in the same graph threw InvalidOperationException on save (fixed by
    /// giving ConfigurationHistoryEntry.Weights its own instance).
    /// </summary>
    [Fact]
    public async Task CreateDefault_SavesWithoutOwnedTypeReferenceAliasing()
    {
        await using var database = Db.New();
        await using var write = database.NewContext();
        write.MatchingConfigurations.Add(MatchingConfiguration.CreateDefault(Db.T0));
        await write.SaveChangesAsync();
    }

    [Fact]
    public async Task RoundTripsConfigurationAndHistory()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var configuration = MatchingConfiguration.CreateDefault(Db.T0);
            configuration.ChangeThreshold(75, Db.Admin, Db.T0.AddMinutes(1));
            write.MatchingConfigurations.Add(configuration);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new MatchingConfigurationRepository(read).GetCurrentAsync();

        loaded.Should().NotBeNull();
        loaded!.MatchThresholdPercent.Should().Be(75);
        loaded.ConfigVersion.Should().Be(2);
        loaded.History.Should().HaveCount(2);
    }
}

public class MatchScoreRepositoryTests
{
    private static readonly MatchingConfigSnapshot Config = MatchingConfigSnapshot.Defaults;

    private static ProfileMatchView Profile(Guid id) =>
        new(id, 1, new[] { "c#" }, EducationLevel.Bachelor, Array.Empty<string>(), "Ramallah", "Ramallah", Array.Empty<WorkArrangement>(), 3m, null, null);

    private static PostingMatchView Posting(Guid id) =>
        new(id, 1, true, new[] { "c#" }, EducationLevel.Bachelor, Array.Empty<string>(), "Ramallah", "Ramallah", WorkArrangement.OnSite, 1, 5, null, null);

    [Fact]
    public async Task RoundTripsScore_AndListsByPostingBestFirst()
    {
        await using var database = Db.New();
        var jobId = Guid.NewGuid();
        var (profileA, profileB) = (Guid.NewGuid(), Guid.NewGuid());
        await using (var write = database.NewContext())
        {
            write.MatchScores.Add(MatchScore.Compute(Profile(profileA), Posting(jobId), Config, ExactSkillSimilarity.Instance, "m1", Db.T0));
            var lowerScoreProfile = Profile(profileB) with { Skills = Array.Empty<string>() };
            write.MatchScores.Add(MatchScore.Compute(lowerScoreProfile, Posting(jobId), Config, ExactSkillSimilarity.Instance, "m1", Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var byPosting = await new MatchScoreRepository(read).ListByPostingAsync(jobId);

        byPosting.Should().HaveCount(2);
        byPosting[0].Score.Should().BeGreaterThanOrEqualTo(byPosting[1].Score);
    }

    [Fact]
    public async Task RemoveByPosting_DeletesAllScoresForThatPosting()
    {
        await using var database = Db.New();
        var jobId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.MatchScores.Add(MatchScore.Compute(Profile(Guid.NewGuid()), Posting(jobId), Config, ExactSkillSimilarity.Instance, "m1", Db.T0));
            await write.SaveChangesAsync();
        }

        await using (var write = database.NewContext())
        {
            var removed = await new MatchScoreRepository(write).RemoveByPostingAsync(jobId);
            await write.SaveChangesAsync();
            removed.Should().Be(1);
        }

        await using var read = database.NewContext();
        (await read.MatchScores.CountAsync()).Should().Be(0);
    }
}

public class CandidateShortlistRepositoryTests
{
    [Fact]
    public async Task RoundTripsShortlistItems()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        var shortlist = CandidateShortlist.Request(Guid.NewGuid(), employer, new Actor(employer, JobPlatform.SharedKernel.Common.Enums.ActorType.Employer),
            10, MatchingConfigSnapshot.Defaults, Db.T0);
        shortlist.Complete(new[] { (Guid.NewGuid(), 80m), (Guid.NewGuid(), 60m) }, Db.T0.AddMinutes(1));
        var id = shortlist.Id;

        await using (var write = database.NewContext())
        {
            write.CandidateShortlists.Add(shortlist);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new CandidateShortlistRepository(read).GetAsync(id);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(ShortlistStatus.Ready);
        loaded.Items.Should().HaveCount(2);
    }
}

public class ResumeParsedDataRepositoryTests
{
    [Fact]
    public async Task ExistsAsync_DetectsDuplicateByResumeAndHash()
    {
        await using var database = Db.New();
        var resumeId = Guid.NewGuid();
        var parserResult = new ParserResult(TextLanguage.En, new[] { new ParsedFieldInput(ParsedFieldName.Skills, "C#", 90) }, new[] { "C#" },
            Array.Empty<string>(), 3, "model-1");
        await using (var write = database.NewContext())
        {
            write.ResumeParsedData.Add(ResumeParsedData.FromParserResult(resumeId, Guid.NewGuid(), "sha-1", parserResult, 70, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var repository = new ResumeParsedDataRepository(read);

        (await repository.ExistsAsync(resumeId, "sha-1")).Should().BeTrue();
        (await repository.ExistsAsync(resumeId, "sha-2")).Should().BeFalse();
    }
}

public class KnownProfileAndPostingRepositoryTests
{
    [Fact]
    public async Task KnownProfile_RoundTrips_AndListsOnlyActiveOnes()
    {
        await using var database = Db.New();
        var active = Guid.NewGuid();
        var deactivated = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.KnownProfiles.Add(KnownProfile.Create(active, Guid.NewGuid(), 1, Db.T0));
            var toDeactivate = KnownProfile.Create(deactivated, Guid.NewGuid(), 1, Db.T0);
            toDeactivate.Deactivate(Db.T0);
            write.KnownProfiles.Add(toDeactivate);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var repository = new KnownProfileRepository(read);
        var activeList = await repository.ListActiveAsync(0, 10);

        activeList.Should().ContainSingle(p => p.Id == active);
    }

    [Fact]
    public async Task KnownPosting_ApplyStatus_PersistsNewStatus()
    {
        await using var database = Db.New();
        var postingId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.KnownPostings.Add(KnownPosting.Create(postingId, Guid.NewGuid(), "draft", "Developer", 1, Db.T0));
            await write.SaveChangesAsync();
        }

        await using (var change = database.NewContext())
        {
            var repository = new KnownPostingRepository(change);
            (await repository.GetAsync(postingId))!.ApplyStatus("active", 2, Db.T0.AddMinutes(1));
            await change.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new KnownPostingRepository(read).GetAsync(postingId))!.Status.Should().Be("active");
    }
}
