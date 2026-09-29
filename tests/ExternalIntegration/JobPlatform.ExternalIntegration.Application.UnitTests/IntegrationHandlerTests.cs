using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Application.Handlers.Integrations;
using JobPlatform.ExternalIntegration.Application.Services.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

public class IntegrationHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    public async Task RegisterExternalJobSiteHandler_ForKnownAccount_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        var store = new FakeStore();
        store.KnownAccounts.Add(new KnownPartnerAccount(partnerId, Kit.Clock().GetUtcNow().UtcDateTime));
        var handler = new RegisterExternalJobSiteHandler(store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new RegisterExternalJobSiteCommand("Jobs4All", "https://jobs4all.example", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdmissionStatus.Should().Be("UnderReview");
        store.Integrations.Should().ContainSingle(i => i.PartnerAccountId == partnerId);
    }

    [Fact]
    public async Task RegisterExternalJobSiteHandler_ForUnknownAccount_ReturnsForbidden()
    {
        var handler = new RegisterExternalJobSiteHandler(new FakeStore(), new FakeStore(), Kit.User(), Kit.Clock());

        var result = await handler.Handle(new RegisterExternalJobSiteCommand("Jobs4All", "https://jobs4all.example", false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCodes.PartnerForbidden);
    }

    [Fact]
    public async Task RegisterExternalJobSiteHandler_WhenAlreadyRegistered_ReturnsConflict()
    {
        var partnerId = Guid.NewGuid();
        var store = new FakeStore();
        store.KnownAccounts.Add(new KnownPartnerAccount(partnerId, Kit.Clock().GetUtcNow().UtcDateTime));
        store.Integrations.Add(ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"),
            false, Kit.Clock().GetUtcNow().UtcDateTime));
        var handler = new RegisterExternalJobSiteHandler(store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new RegisterExternalJobSiteCommand("Jobs4All", "https://jobs4all.example", false), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.AlreadyRegistered);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    public async Task ApproveAndActivate_HappyPath()
    {
        var store = new FakeStore();
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), Guid.NewGuid(), new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"),
            false, Kit.Clock().GetUtcNow().UtcDateTime);
        store.Integrations.Add(integration);
        var approveHandler = new ApproveExternalJobSiteHandler(store, Kit.User(SharedKernel.Common.Enums.ActorType.Administrator), Kit.Clock());
        var activateHandler = new ActivateIntegrationHandler(store, Kit.User(SharedKernel.Common.Enums.ActorType.Administrator));

        var approve = await approveHandler.Handle(new ApproveExternalJobSiteCommand(integration.Id, "MoL manual review"), CancellationToken.None);
        var activate = await activateHandler.Handle(new ActivateIntegrationCommand(integration.Id), CancellationToken.None);

        approve.IsSuccess.Should().BeTrue();
        activate.IsSuccess.Should().BeTrue();
        integration.Status.Should().Be(IntegrationStatus.Active);
    }

    [Fact]
    public async Task ApproveExternalJobSiteHandler_UnknownId_ReturnsNotFound()
    {
        var handler = new ApproveExternalJobSiteHandler(new FakeStore(), Kit.User(SharedKernel.Common.Enums.ActorType.Administrator), Kit.Clock());

        var result = await handler.Handle(new ApproveExternalJobSiteCommand(Guid.NewGuid(), "basis"), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }

    private static (FakeStore Store, ExternalJobSiteIntegration Integration) ActiveIntegration(Guid? partnerId = null)
    {
        var store = new FakeStore();
        var pid = partnerId ?? Guid.NewGuid();
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), pid, new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"), true,
            Kit.Clock().GetUtcNow().UtcDateTime);
        integration.Activate(new Actor(Guid.NewGuid(), true));
        store.Integrations.Add(integration);
        return (store, integration);
    }

    [Fact]
    [Trait("Story", "US-2.5-02")]
    public async Task EnableIntegrationHandler_ForOwner_EnablesModels()
    {
        var partnerId = Guid.NewGuid();
        var (store, integration) = ActiveIntegration(partnerId);
        var handler = new EnableIntegrationHandler(store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new EnableIntegrationCommand(true, true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        integration.Models.PullEnabled.Should().BeTrue();
        integration.Models.PushEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-13")]
    public async Task ConfigureAttributionVisibilityHandler_ForOwner_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        var (store, integration) = ActiveIntegration(partnerId);
        var handler = new ConfigureAttributionVisibilityHandler(store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new ConfigureAttributionVisibilityCommand("AdminOnly"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        integration.AttributionVisibility.Should().Be(AttributionVisibilityValue.AdminOnly);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-05")]
    public async Task ProvisionSandboxHandler_WithActiveCredential_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        var (store, integration) = ActiveIntegration(partnerId);
        store.Credentials.Add(new PartnerCredential(Guid.NewGuid(), partnerId, Kit.Clock().GetUtcNow().UtcDateTime.AddDays(30), 1));
        var handler = new ProvisionSandboxHandler(store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new ProvisionSandboxCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        integration.Sandbox.Should().Be(SandboxState.Provisioned);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-05")]
    [Trait("AC", "AC-02")]
    public async Task ProvisionSandboxHandler_WithoutCredential_ThrowsForbidden()
    {
        var partnerId = Guid.NewGuid();
        var (store, _) = ActiveIntegration(partnerId);
        var handler = new ProvisionSandboxHandler(store, store, Kit.User(id: partnerId), Kit.Clock());

        var act = () => handler.Handle(new ProvisionSandboxCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<SharedKernel.Domain.BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.PartnerForbidden);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-02")]
    public async Task StartSyncRunHandler_WithNoPartnerData_CompletesWithZeroCounts()
    {
        var partnerId = Guid.NewGuid();
        var (store, _) = ActiveIntegration(partnerId);
        var feed = new FakePartnerJobFeedClient();
        var orchestrator = new PartnerSyncOrchestrator(store, store, store, feed, Kit.Clock());
        var handler = new StartSyncRunHandler(store, store, orchestrator, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new StartSyncRunCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");
        result.Value.Received.Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-02")]
    [Trait("AC", "AC-03")]
    public async Task StartSyncRunHandler_WhenFeedTimesOut_FailsRunButStillReturnsIt()
    {
        var partnerId = Guid.NewGuid();
        var (store, integration) = ActiveIntegration(partnerId);
        var feed = new FakePartnerJobFeedClient { ThrowTimeout = true };
        var orchestrator = new PartnerSyncOrchestrator(store, store, store, feed, Kit.Clock());
        var handler = new StartSyncRunHandler(store, store, orchestrator, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new StartSyncRunCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCodes.UpstreamTimeout);
        integration.CurrentRun.Should().BeNull();
        integration.SyncRuns.Should().ContainSingle(r => r.Status == SyncRunStatus.Failed);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-02")]
    public async Task StartSyncRunHandler_WithPartnerData_AcceptsViaMapping()
    {
        var partnerId = Guid.NewGuid();
        var (store, integration) = ActiveIntegration(partnerId);
        var mapping = JobDataMapping.Create(Guid.NewGuid(), integration.Id, new[]
        {
            new MappingRule("t", "title", MappingTransform.None), new MappingRule("s", "summary", MappingTransform.None),
            new MappingRule("sk", "skills", MappingTransform.SplitComma)
        }, "v1", Guid.NewGuid(), Kit.Clock().GetUtcNow().UtcDateTime);
        store.Mappings.Add(mapping);
        var feed = new FakePartnerJobFeedClient
        {
            NextPayloads = new[]
            {
                new PartnerJobPayload("src-1", new Dictionary<string, string> { ["t"] = "Dev", ["s"] = "Summary", ["sk"] = "C#" })
            }
        };
        var orchestrator = new PartnerSyncOrchestrator(store, store, store, feed, Kit.Clock());
        var handler = new StartSyncRunHandler(store, store, orchestrator, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new StartSyncRunCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Accepted.Should().Be(1);
        store.JobData.Should().ContainSingle(j => j.SourceJobId == "src-1" && j.Status == JobDataStatus.Accepted);
        store.Attributions.Should().ContainSingle();
    }
}
