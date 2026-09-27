using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain.UnitTests;

public class ExternalJobSiteIntegrationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static SourcePlatform Platform() => new(Guid.NewGuid(), "Jobs4All", "https://jobs4all.example");

    private static ExternalJobSiteIntegration Registered(Guid? partnerAccountId = null, bool recommended = false) =>
        ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerAccountId ?? Guid.NewGuid(), Platform(), recommended, At);

    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    [Trait("AC", "AC-01")]
    public void Register_Recommended_IsAutoApproved()
    {
        var integration = Registered(recommended: true);

        integration.AdmissionStatus.Should().Be(AdmissionStatus.Approved);
        integration.Status.Should().Be(IntegrationStatus.Registered);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    [Trait("AC", "AC-02")]
    public void Register_NotRecommended_GoesToUnderReview()
    {
        var integration = Registered(recommended: false);

        integration.AdmissionStatus.Should().Be(AdmissionStatus.UnderReview);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    [Trait("AC", "AC-03")]
    public void Activate_WithoutApproval_ThrowsNotApproved()
    {
        var integration = Registered(recommended: false);

        var act = () => integration.Activate(Admin);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.IntegrationNotApproved);
        ex.ExternalCode.Should().Be(ErrorCodes.NotApproved);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    [Trait("AC", "AC-04")]
    public void ApproveByMolPef_ThenActivate_Succeeds()
    {
        var integration = Registered(recommended: false);

        integration.ApproveByMolPef(Admin, "Manual MoL review", At);
        integration.Activate(Admin);

        integration.AdmissionStatus.Should().Be(AdmissionStatus.Approved);
        integration.ApprovalBasis.Should().Be("Manual MoL review");
        integration.Status.Should().Be(IntegrationStatus.Active);
    }

    [Fact]
    public void ApproveByMolPef_ByNonAdministrator_ThrowsForbidden()
    {
        var integration = Registered();

        var act = () => integration.ApproveByMolPef(new Actor(Guid.NewGuid(), false), "basis", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationAdminOnly);
    }

    [Fact]
    public void Suspend_FromActive_Succeeds()
    {
        var integration = Registered(recommended: true);
        integration.Activate(Admin);

        integration.Suspend(Admin, "policy violation");

        integration.Status.Should().Be(IntegrationStatus.Suspended);
    }

    [Fact]
    public void Suspend_WhenNotActive_ThrowsNotActive()
    {
        var integration = Registered(recommended: true);

        var act = () => integration.Suspend(Admin, "reason");

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationNotActive);
    }

    [Fact]
    [Trait("Story", "US-2.5-02")]
    [Trait("AC", "AC-04")]
    public void Enable_WhenActiveWithAtLeastOneModel_RaisesSupportedEvent()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);
        integration.Activate(Admin);

        integration.Enable(true, false, new Actor(partnerId, false), At);

        integration.Models.PullEnabled.Should().BeTrue();
        integration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ExternalJobSiteIntegrationSupportedDomainEvent>()
            .Which.Models.Should().BeEquivalentTo(new[] { "pull" });
    }

    [Fact]
    public void Enable_WithNeitherModel_ThrowsBusinessRule()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);
        integration.Activate(Admin);

        var act = () => integration.Enable(false, false, new Actor(partnerId, false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationAtLeastOneModel);
    }

    [Fact]
    public void Enable_ByNonOwner_ThrowsForbidden()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);
        integration.Activate(Admin);

        var act = () => integration.Enable(true, true, new Actor(Guid.NewGuid(), false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.PartnerForbidden);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-13")]
    [Trait("AC", "AC-04")]
    public void ConfigureAttributionVisibility_ByOwner_RaisesEvent()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId);

        integration.ConfigureAttributionVisibility(AttributionVisibilityValue.AdminOnly, new Actor(partnerId, false), At);

        integration.AttributionVisibility.Should().Be(AttributionVisibilityValue.AdminOnly);
        integration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AttributionVisibilityConfiguredDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-13")]
    [Trait("AC", "AC-04")]
    public void ConfigureAttributionVisibility_ByNonOwner_ThrowsForbidden()
    {
        var integration = Registered();

        var act = () => integration.ConfigureAttributionVisibility(AttributionVisibilityValue.Public, new Actor(Guid.NewGuid(), false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationOwnerOnly);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-05")]
    [Trait("AC", "AC-01")]
    public void ProvisionSandbox_WithActiveCredential_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId);

        integration.ProvisionSandbox(true, new Actor(partnerId, false), At);

        integration.Sandbox.Should().Be(SandboxState.Provisioned);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-05")]
    [Trait("AC", "AC-02")]
    public void ProvisionSandbox_WithoutActiveCredential_ThrowsForbidden()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId);

        var act = () => integration.ProvisionSandbox(false, new Actor(partnerId, false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationNoActiveCredential);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-02")]
    public void StartSyncRun_WhileOneIsRunning_ThrowsRunInProgress()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);
        integration.Activate(Admin);
        var actor = new Actor(partnerId, false);
        integration.StartSyncRun(SyncTrigger.OnDemand, 1, actor, At);

        var act = () => integration.StartSyncRun(SyncTrigger.OnDemand, 1, actor, At.AddMinutes(1));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationRunInProgress);
    }

    [Fact]
    public void CompleteSyncRun_SetsCountsAndClearsCurrentRun()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);
        integration.Activate(Admin);
        var actor = new Actor(partnerId, false);
        var run = integration.StartSyncRun(SyncTrigger.OnDemand, 1, actor, At);

        integration.CompleteSyncRun(run.Id, 5, 4, 1, At.AddMinutes(2));

        integration.CurrentRun.Should().BeNull();
        run.Status.Should().Be(SyncRunStatus.Completed);
        run.Accepted.Should().Be(4);
        run.Rejected.Should().Be(1);
    }

    [Fact]
    public void FailSyncRun_SetsFailedStatusAndErrorCode()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);
        integration.Activate(Admin);
        var actor = new Actor(partnerId, false);
        var run = integration.StartSyncRun(SyncTrigger.OnDemand, 1, actor, At);

        integration.FailSyncRun(run.Id, ErrorCodes.UpstreamTimeout, At.AddMinutes(1));

        run.Status.Should().Be(SyncRunStatus.Failed);
        run.ErrorCode.Should().Be(ErrorCodes.UpstreamTimeout);
        integration.CurrentRun.Should().BeNull();
    }

    [Fact]
    public void StartSyncRun_WhenNotActive_ThrowsNotActive()
    {
        var partnerId = Guid.NewGuid();
        var integration = Registered(partnerId, recommended: true);

        var act = () => integration.StartSyncRun(SyncTrigger.OnDemand, 1, new Actor(partnerId, false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.IntegrationNotActive);
    }
}
