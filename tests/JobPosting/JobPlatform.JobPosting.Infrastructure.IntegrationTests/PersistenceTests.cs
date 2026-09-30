using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobPosting.Application.Events;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Infrastructure.Persistence;
using JobPlatform.JobPosting.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static SqliteTestDatabase<JobPostingDbContext> New() => new(o => new JobPostingDbContext(o), new JobPostingEventMapper());

    public static JobPostingFields Fields() => new(
        new LocalizedText("عنوان الوظيفة", "Job Title"), new LocalizedText("وصف طويل بما فيه الكفاية لتجاوز الحد الأدنى للتحقق.",
            "A sufficiently long summary that passes the minimum length validation."),
        new[] { "csharp", "sql" }, "software-development", ContractType.FullTime, EducationLevel.Bachelor, new[] { "clean-code" }, WorkFormat.Hybrid,
        JobLocation.Create("Ramallah", "Ramallah"), SalaryRange.Create(1000, 2000, "ILS"), 1, 5, new[] { "ar", "en" },
        ApplicationDeadline.Create(T0.AddDays(30), true), "https://example.com/apply", new Dictionary<string, string> { ["gender"] = "any" });
}

public class PersistenceTests
{
    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public async Task JobPosting_RoundTripsAllOwnedTypesAndCollections()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(employerId, Db.Fields(), new Actor(employerId, false, false), 3,
            ContentHasher.Hash(Db.Fields()), Db.T0);
        await using (var write = database.NewContext())
        {
            write.JobPostings.Add(posting);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new JobPostingRepository(read).GetByIdAsync(posting.Id);

        loaded.Should().NotBeNull();
        loaded!.Title.En.Should().Be("Job Title");
        loaded.Skills.Should().BeEquivalentTo(new[] { "csharp", "sql" });
        loaded.RequiredTraining.Should().BeEquivalentTo(new[] { "clean-code" });
        loaded.Location!.City.Should().Be("Ramallah");
        loaded.Salary!.Min.Should().Be(1000);
        loaded.OtherFields.Should().ContainKey("gender").WhoseValue.Should().Be("any");
        loaded.Visibility.Scope.Should().Be(VisibilityScope.Public);
        loaded.Source.Type.Should().Be(JobSourceType.Employer);
        loaded.RowVersion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task JobPosting_WithNullLocationAndSalary_RoundTripsAsNull()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        var fields = Db.Fields() with { Location = null, Salary = null };
        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(employerId, fields, new Actor(employerId, false, false), 1,
            ContentHasher.Hash(fields), Db.T0);
        await using (var write = database.NewContext())
        {
            write.JobPostings.Add(posting);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await read.JobPostings.AsNoTracking().SingleAsync();

        loaded.Location.Should().BeNull();
        loaded.Salary.Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public async Task SavingAPosting_WritesAnOutboxRow_InTheSameTransaction()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(employerId, Db.Fields(), new Actor(employerId, false, false), 1,
            ContentHasher.Hash(Db.Fields()), Db.T0);

        await using var write = database.NewContext();
        write.JobPostings.Add(posting);
        await write.SaveChangesAsync();

        var outbox = await write.Set<OutboxMessage>().SingleAsync();
        outbox.Type.Should().Be("JobPostingCreated");
        outbox.Exchange.Should().Be("jobplatform.job-posting.events");
        outbox.Status.Should().Be(OutboxStatus.Pending);
    }

    [Fact]
    public async Task FavoriteJobList_ToggleTwice_RoundTripsEmptyList()
    {
        await using var database = Db.New();
        var owner = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var list = FavoriteJobList.CreateEmpty(owner);
        list.Toggle(jobId, new Actor(owner, false, false), Db.T0);
        await using (var write = database.NewContext())
        {
            write.FavoriteJobLists.Add(list);
            await write.SaveChangesAsync();
        }

        await using (var toggle = database.NewContext())
        {
            var loaded = await new FavoriteJobListRepository(toggle).GetByOwnerAsync(owner);
            loaded!.Toggle(jobId, new Actor(owner, false, false), Db.T0);
            await toggle.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var final = await new FavoriteJobListRepository(read).GetByOwnerAsync(owner);
        final!.JobPostingIds.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.2.2-04")]
    [Trait("AC", "AC-02")]
    public async Task SavedSearch_RoundTripsCriteriaAndEnforcesUniqueOwnerHash()
    {
        await using var database = Db.New();
        var owner = Guid.NewGuid();
        var criteria = new SearchCriteria("developer", "Ramallah", null, 1000, 2000, ContractType.FullTime, null, null, "software-development");
        var search = SavedSearch.Save(owner, criteria, true, Db.T0);
        await using (var write = database.NewContext())
        {
            write.SavedSearches.Add(search);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new SavedSearchRepository(read).GetByHashAsync(owner, SavedSearch.Hash(criteria));
        loaded!.Criteria.Keyword.Should().Be("developer");
        loaded.Criteria.ContractType.Should().Be(ContractType.FullTime);

        await using var dup = database.NewContext();
        dup.SavedSearches.Add(SavedSearch.Save(owner, criteria, false, Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }
}
