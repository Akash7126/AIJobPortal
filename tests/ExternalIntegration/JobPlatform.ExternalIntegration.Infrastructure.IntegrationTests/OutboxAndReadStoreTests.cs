using System.Text.Json.Nodes;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.IntegrationTests;

public class OutboxTests
{
    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-05")]
    public async Task Accept_WritesTheOutboxRowInTheSameSaveAsTheAggregate()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var jobData = JobData.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "job-1", "{}", JobDataModel.Push, Db.T0);
        jobData.Standardize(new StandardJob("Title", "Summary", new[] { "C#" }, "FullTime", "Remote", null, "Ramallah", null));
        jobData.Accept(Guid.NewGuid(), "Public", Db.T0);
        db.JobData.Add(jobData);

        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        (row.Type, row.Exchange, row.RoutingKey, row.Status).Should().Be(
            ("JobDataImported", "jobplatform.external-integration.events", "job-data.imported.v1", OutboxStatus.Pending));
        var payload = JsonNode.Parse(row.Payload)!;
        payload["platformJobId"]!.GetValue<string>().Should().Be(jobData.PlatformJobId);
        payload["title"]!.GetValue<string>().Should().Be("Title");
    }

    [Fact]
    public async Task ConfigureAttributionVisibility_WritesTheOutboxRow()
    {
        await using var database = Db.New();
        var partnerId = Guid.NewGuid();
        await using var db = database.NewContext();
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"),
            true, Db.T0);
        db.Integrations.Add(integration);
        await db.SaveChangesAsync();

        integration.ConfigureAttributionVisibility(AttributionVisibilityValue.AdminOnly, new Actor(partnerId, false), Db.T0.AddMinutes(1));
        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        row.Type.Should().Be("AttributionVisibilityConfigured");
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        await db.BeginTransactionAsync();
        db.Integrations.Add(ExternalJobSiteIntegration.Register(Guid.NewGuid(), Guid.NewGuid(),
            new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"), true, Db.T0));
        await db.SaveChangesAsync();
        await db.RollbackTransactionAsync();

        (await db.Integrations.CountAsync()).Should().Be(0);
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }
}

public class ReadStoreTests
{
    [Fact]
    public async Task GetIntegrationSummary_ReturnsRecentRuns()
    {
        await using var database = Db.New();
        var sourcePlatformId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId,
                new SourcePlatform(sourcePlatformId, "Jobs4All", "https://jobs4all.example"), true, Db.T0);
            integration.Activate(Db.Admin);
            var run = integration.StartSyncRun(SyncTrigger.OnDemand, 1, new Actor(partnerId, false), Db.T0);
            integration.CompleteSyncRun(run.Id, 2, 2, 0, Db.T0.AddMinutes(1));
            write.Integrations.Add(integration);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new ExternalIntegrationReadStore(read);
        var summary = await store.GetIntegrationSummaryAsync(sourcePlatformId);

        summary.Should().NotBeNull();
        summary!.RecentRuns.Should().ContainSingle(r => r.Accepted == 2);
    }

    [Fact]
    public async Task ListSoftwareInterfaces_OnlyReturnsEnabled()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var enabled = SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.ExternalJobSite, "A", "https://a.example",
                Db.Admin);
            var disabled = SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.ExternalJobSite, "B", "https://b.example",
                Db.Admin);
            disabled.SetEnabled(false, Db.Admin);
            write.SoftwareInterfaces.AddRange(enabled, disabled);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new ExternalIntegrationReadStore(read);
        var list = await store.ListSoftwareInterfacesAsync();

        list.Should().ContainSingle(v => v.Name == "A");
    }
}
