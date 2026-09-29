using JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;
using JobPlatform.ExternalIntegration.Application.Handlers.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

public class JobDataFlowHandlerTests
{
    private static (FakeStore Store, ExternalJobSiteIntegration Integration) ActivePushIntegration(Guid partnerId)
    {
        var store = new FakeStore();
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"), true,
            Kit.Clock().GetUtcNow().UtcDateTime);
        integration.Activate(new Actor(Guid.NewGuid(), true));
        integration.Enable(false, true, new Actor(partnerId, false), Kit.Clock().GetUtcNow().UtcDateTime);
        store.Integrations.Add(integration);
        return (store, integration);
    }

    private static PushJobDataCommand ValidPush(string sourceJobId = "src-1") => new(sourceJobId, "Backend Engineer", "Build things.",
        new[] { "C#", "SQL" }, "FullTime", "Remote", DateTime.UtcNow.AddMonths(1), "Ramallah", "https://x.example/jobs/1", null);

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-01")]
    public async Task PushJobDataHandler_FirstPush_CreatesJobAndAttribution()
    {
        var partnerId = Guid.NewGuid();
        var (store, _) = ActivePushIntegration(partnerId);
        var handler = new PushJobDataHandler(store, store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(ValidPush(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().BeTrue();
        store.JobData.Should().ContainSingle(j => j.Status == JobDataStatus.Accepted);
        store.Attributions.Should().ContainSingle(a => a.PlatformJobId == result.Value.PlatformJobId);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-04")]
    public async Task PushJobDataHandler_RePush_UpsertsInsteadOfDuplicating()
    {
        var partnerId = Guid.NewGuid();
        var (store, _) = ActivePushIntegration(partnerId);
        var handler = new PushJobDataHandler(store, store, store, Kit.User(id: partnerId), Kit.Clock());
        var first = await handler.Handle(ValidPush(), CancellationToken.None);

        var second = await handler.Handle(ValidPush() with { Title = "Senior Backend Engineer" }, CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.Created.Should().BeFalse();
        second.Value.PlatformJobId.Should().Be(first.Value.PlatformJobId);
        store.JobData.Should().ContainSingle();
        store.Attributions.Should().ContainSingle("re-pushing must not create a second attribution");
    }

    [Fact]
    public async Task PushJobDataHandler_WhenPushNotEnabled_ReturnsForbidden()
    {
        var partnerId = Guid.NewGuid();
        var store = new FakeStore();
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"), true,
            Kit.Clock().GetUtcNow().UtcDateTime);
        integration.Activate(new Actor(Guid.NewGuid(), true));
        store.Integrations.Add(integration);
        var handler = new PushJobDataHandler(store, store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(ValidPush(), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.IntegrationNotActive);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    public async Task SyncJobPostAttributionHandler_Close_TransitionsAttribution()
    {
        var store = new FakeStore();
        var attribution = JobPostAttribution.Tag(Guid.NewGuid(), Guid.NewGuid(), "plat-1", "Jobs4All", null, Guid.NewGuid(),
            Kit.Clock().GetUtcNow().UtcDateTime);
        store.Attributions.Add(attribution);
        var handler = new SyncJobPostAttributionHandler(store, Kit.User(), Kit.Clock());

        var result = await handler.Handle(new SyncJobPostAttributionCommand("plat-1", "Close", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        attribution.SyncState.Should().Be(AttributionSyncState.Closed);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-03")]
    public async Task SyncJobPostAttributionHandler_AfterClose_Throws()
    {
        var store = new FakeStore();
        var attribution = JobPostAttribution.Tag(Guid.NewGuid(), Guid.NewGuid(), "plat-1", "Jobs4All", null, Guid.NewGuid(),
            Kit.Clock().GetUtcNow().UtcDateTime);
        attribution.Close(new Actor(Guid.NewGuid(), false), Kit.Clock().GetUtcNow().UtcDateTime);
        store.Attributions.Add(attribution);
        var handler = new SyncJobPostAttributionHandler(store, Kit.User(), Kit.Clock());

        var act = () => handler.Handle(new SyncJobPostAttributionCommand("plat-1", "EditDescription", null, "new text"), CancellationToken.None);

        (await act.Should().ThrowAsync<SharedKernel.Domain.BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.AttributionStateClosed);
    }

    [Fact]
    public async Task SyncJobPostAttributionHandler_UnknownPlatformJobId_ReturnsNotFound()
    {
        var handler = new SyncJobPostAttributionHandler(new FakeStore(), Kit.User(), Kit.Clock());

        var result = await handler.Handle(new SyncJobPostAttributionCommand("missing", "Close", null, null), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }
}
