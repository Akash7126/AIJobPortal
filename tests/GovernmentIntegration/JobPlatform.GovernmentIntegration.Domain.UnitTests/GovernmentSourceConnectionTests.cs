using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class GovernmentSourceConnectionTests
{
    private static readonly Actor Admin = new(Guid.NewGuid(), true);
    private static readonly Actor NonAdmin = new(Guid.NewGuid(), false);
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    [Trait("AC", "AC-04")]
    public void Configure_ByAdministrator_StartsHealthy()
    {
        var connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), Admin, SourceSystem.MoL, "https://mol.example/api", "ApiKey", "secret-ref", true);

        connection.Health.Should().Be(ConnectionHealth.Healthy);
        connection.Enabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    [Trait("AC", "AC-04")]
    public void Configure_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => GovernmentSourceConnection.Configure(Guid.NewGuid(), NonAdmin, SourceSystem.MoL, "https://mol.example/api", "ApiKey", "ref", true);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ConnectionAdminOnly);
        ex.ExternalCode.Should().Be("E-GI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-2.5-04")]
    [Trait("AC", "AC-04")]
    public void Update_ReappliedConfiguration_IsIdempotent()
    {
        var connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), Admin, SourceSystem.PEF, "https://pef.example/api", "ApiKey", "ref", true);

        connection.Update(Admin, "https://pef.example/api", "ApiKey", "ref", true);
        connection.Update(Admin, "https://pef.example/api", "ApiKey", "ref", true);

        connection.Endpoint.Should().Be("https://pef.example/api");
        connection.Enabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-2.5-01")]
    [Trait("AC", "AC-02")]
    public void RecordSyncFailure_ThreeTimes_DegradesHealth()
    {
        var connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), Admin, SourceSystem.MoL, "https://mol.example/api", "ApiKey", "ref", true);

        connection.RecordSyncFailure();
        connection.RecordSyncFailure();
        connection.RecordSyncFailure();

        connection.Health.Should().Be(ConnectionHealth.Degraded);
    }

    [Fact]
    [Trait("Story", "US-2.5-01")]
    [Trait("AC", "AC-03")]
    public void RecordSyncSuccess_SetsLastKnownGoodAndResetsFailures()
    {
        var connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), Admin, SourceSystem.MoL, "https://mol.example/api", "ApiKey", "ref", true);
        connection.RecordSyncFailure();

        connection.RecordSyncSuccess("snapshot-1", At);

        connection.Health.Should().Be(ConnectionHealth.Healthy);
        connection.LastKnownGood!.SnapshotRef.Should().Be("snapshot-1");
        connection.LastSuccessfulSyncAtUtc.Should().Be(At);
    }
}
