using JobPlatform.ExternalIntegration.Application.Events;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;

namespace JobPlatform.ExternalIntegration.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Actor Admin = new(Guid.NewGuid(), true);

    public static SqliteTestDatabase<ExternalIntegrationDbContext> New() =>
        new(o => new ExternalIntegrationDbContext(o), new ExternalIntegrationEventMapper());
}

public class RepositoryTests
{
    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    public async Task ExternalJobSiteIntegration_RoundTripsOwnedValueObjectsAndSyncRuns()
    {
        await using var database = Db.New();
        var partnerId = Guid.NewGuid();
        var platform = new SourcePlatform(Guid.NewGuid(), "Jobs4All", "https://jobs4all.example");
        Guid integrationId;
        Guid runId;
        await using (var write = database.NewContext())
        {
            var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, platform, true, Db.T0);
            integration.Activate(Db.Admin);
            var run = integration.StartSyncRun(SyncTrigger.OnDemand, 1, new Actor(partnerId, false), Db.T0);
            integration.CompleteSyncRun(run.Id, 3, 2, 1, Db.T0.AddMinutes(5));
            integrationId = integration.Id;
            runId = run.Id;
            write.Integrations.Add(integration);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new ExternalJobSiteIntegrationRepository(read).GetByPartnerAccountAsync(partnerId);
        loaded!.Id.Should().Be(integrationId);
        loaded.SourcePlatform.Name.Should().Be("Jobs4All");
        loaded.Status.Should().Be(IntegrationStatus.Active);
        loaded.SyncRuns.Should().ContainSingle(r => r.Id == runId && r.Accepted == 2 && r.Status == SyncRunStatus.Completed);
    }

    [Fact]
    public async Task ExternalJobSiteIntegration_SamePartnerAccountTwice_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        var partnerId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.Integrations.Add(ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "A", "https://a.example"),
            true, Db.T0));
        await db.SaveChangesAsync();
        db.Integrations.Add(ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "B", "https://b.example"),
            true, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-04")]
    public async Task JobData_SameSourcePlatformAndSourceJobIdTwice_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        var sourcePlatformId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.JobData.Add(JobData.Receive(Guid.NewGuid(), Guid.NewGuid(), sourcePlatformId, "job-1", "{}", JobDataModel.Push, Db.T0));
        await db.SaveChangesAsync();
        db.JobData.Add(JobData.Receive(Guid.NewGuid(), Guid.NewGuid(), sourcePlatformId, "job-1", "{}", JobDataModel.Push, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    public async Task JobData_RoundTripsStandardizedJson()
    {
        await using var database = Db.New();
        Guid id;
        await using (var write = database.NewContext())
        {
            var jobData = JobData.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "job-1", "{}", JobDataModel.Push, Db.T0);
            jobData.Standardize(new StandardJob("Title", "Summary", new[] { "C#", "SQL" }, "FullTime", "Remote", Db.T0.AddMonths(1), "Ramallah",
                "https://partner.example/1"));
            jobData.Accept(Guid.NewGuid(), "Public", Db.T0);
            id = jobData.Id;
            write.JobData.Add(jobData);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new JobDataRepository(read).GetByIdAsync(id);
        loaded!.Standardized!.Skills.Should().BeEquivalentTo(new[] { "C#", "SQL" });
        loaded.PlatformJobId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    public async Task JobPostAttribution_SamePlatformJobIdTwice_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.JobPostAttributions.Add(JobPostAttribution.Tag(Guid.NewGuid(), Guid.NewGuid(), "plat-1", "X", null, Guid.NewGuid(), Db.T0));
        await db.SaveChangesAsync();
        db.JobPostAttributions.Add(JobPostAttribution.Tag(Guid.NewGuid(), Guid.NewGuid(), "plat-1", "X", null, Guid.NewGuid(), Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-04")]
    public async Task JobDataMapping_RoundTripsRulesJson()
    {
        await using var database = Db.New();
        var integrationId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var mapping = JobDataMapping.Create(Guid.NewGuid(), integrationId, new[]
            {
                new MappingRule("t", "title", MappingTransform.Trim), new MappingRule("s", "summary", MappingTransform.None),
                new MappingRule("sk", "skills", MappingTransform.SplitComma)
            }, "v1", Guid.NewGuid(), Db.T0);
            write.JobDataMappings.Add(mapping);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new JobDataMappingRepository(read).GetByIntegrationAsync(integrationId);
        loaded!.Rules.Should().HaveCount(3);
        loaded.Rules.Should().Contain(r => r.TargetField == "skills" && r.Transform == MappingTransform.SplitComma);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-01")]
    public async Task ApiVersion_RoundTripsAcceptedFormats()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var version = ApiVersion.Release("v1");
            version.ConfigureAcceptedFormats(new[] { "json", "xml" }, Db.Admin);
            write.ApiVersions.Add(version);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new ApiVersionRepository(read).GetAsync("v1");
        loaded!.AcceptedFormats.Should().BeEquivalentTo(new[] { "json", "xml" });
    }

    [Fact]
    [Trait("Story", "US-4.3-01")]
    public async Task SoftwareInterface_SameCategoryAndNameTwice_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.SoftwareInterfaces.Add(SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.ExternalJobSite, "X",
            "https://x.example", Db.Admin));
        await db.SaveChangesAsync();
        db.SoftwareInterfaces.Add(SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.ExternalJobSite, "X",
            "https://x.example/v2", Db.Admin));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task PartnerCredential_RoundTrips()
    {
        await using var database = Db.New();
        var credentialId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.PartnerCredentials.Add(new PartnerCredential(credentialId, accountId, Db.T0.AddDays(30), 1));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new PartnerCredentialRepository(read).HasActiveCredentialAsync(accountId, Db.T0)).Should().BeTrue();
        (await new PartnerCredentialRepository(read).GetByIdAsync(credentialId))!.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task KnownPartnerAccount_RoundTrips()
    {
        await using var database = Db.New();
        var accountId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.KnownPartnerAccounts.Add(new KnownPartnerAccount(accountId, Db.T0));
            await write.SaveChangesAsync();
        }

        (await new KnownPartnerAccountRepository(database.NewContext()).GetAsync(accountId)).Should().NotBeNull();
        (await new KnownPartnerAccountRepository(database.NewContext()).GetAsync(Guid.NewGuid())).Should().BeNull();
    }
}
