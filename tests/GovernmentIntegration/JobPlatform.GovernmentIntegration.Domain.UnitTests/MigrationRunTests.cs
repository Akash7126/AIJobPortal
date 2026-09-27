using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class MigrationRunTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);
    private static readonly Actor NonAdmin = new(Guid.NewGuid(), false);

    [Fact]
    [Trait("Story", "US-6.1-01")]
    [Trait("AC", "AC-04")]
    public void Start_ByAdministrator_BeginsFirstPhaseAndLogsIt()
    {
        var run = MigrationRun.Start(Guid.NewGuid(), Admin, new[] { "Import", "Cleanse" }, At);

        run.Status.Should().Be(MigrationStatus.Running);
        run.CurrentPhase!.Name.Should().Be("Import");
        run.CurrentPhase.Status.Should().Be(MigrationPhaseStatus.Running);
        run.Log.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-6.1-01")]
    [Trait("AC", "AC-04")]
    public void Start_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => MigrationRun.Start(Guid.NewGuid(), NonAdmin, new[] { "Import" }, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.MigrationAdminOnly);
        ex.ExternalCode.Should().Be("E-GI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-6.1-03")]
    [Trait("AC", "AC-01")]
    public void AcceptPhase_OnLastPhase_CompletesTheRun()
    {
        var run = MigrationRun.Start(Guid.NewGuid(), Admin, new[] { "Import" }, At);

        run.AcceptPhase("passed", At);

        run.Status.Should().Be(MigrationStatus.Completed);
        run.Phases.Single().Status.Should().Be(MigrationPhaseStatus.Accepted);
    }

    [Fact]
    [Trait("Story", "US-6.1-03")]
    [Trait("AC", "AC-01")]
    public void AcceptPhase_NotOnLastPhase_AdvancesToNextPhase()
    {
        var run = MigrationRun.Start(Guid.NewGuid(), Admin, new[] { "Import", "Cleanse" }, At);

        run.AcceptPhase("passed", At);

        run.Status.Should().Be(MigrationStatus.Running);
        run.CurrentPhaseIndex.Should().Be(1);
        run.CurrentPhase!.Name.Should().Be("Cleanse");
    }

    [Fact]
    [Trait("Story", "US-6.1-03")]
    [Trait("AC", "AC-02")]
    public void FailPhase_SetsPhaseFailedAndDoesNotAdvance()
    {
        var run = MigrationRun.Start(Guid.NewGuid(), Admin, new[] { "Import", "Cleanse" }, At);

        run.FailPhase("Schema mismatch", At);

        run.Status.Should().Be(MigrationStatus.PhaseFailed);
        run.CurrentPhaseIndex.Should().Be(0);
        run.CurrentPhase!.Status.Should().Be(MigrationPhaseStatus.Failed);
    }

    [Fact]
    [Trait("Story", "US-6.1-03")]
    [Trait("AC", "AC-03")]
    public void Rollback_ByAdministratorAfterFailure_SetsRolledBack()
    {
        var run = MigrationRun.Start(Guid.NewGuid(), Admin, new[] { "Import" }, At);
        run.FailPhase("boom", At);

        run.Rollback(Admin, At);

        run.Status.Should().Be(MigrationStatus.RolledBack);
    }

    [Fact]
    [Trait("Story", "US-6.1-03")]
    [Trait("AC", "AC-03")]
    public void Rollback_ByNonAdministrator_ThrowsForbidden()
    {
        var run = MigrationRun.Start(Guid.NewGuid(), Admin, new[] { "Import" }, At);
        run.FailPhase("boom", At);

        var act = () => run.Rollback(NonAdmin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.MigrationAdminOnly);
    }
}
