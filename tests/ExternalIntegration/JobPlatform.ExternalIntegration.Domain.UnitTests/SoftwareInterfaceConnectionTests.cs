using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain.UnitTests;

public class SoftwareInterfaceConnectionTests
{
    private static readonly Actor Admin = new(Guid.NewGuid(), true);
    private static readonly Actor NonAdmin = new(Guid.NewGuid(), false);

    [Fact]
    [Trait("Story", "US-4.3-01")]
    [Trait("AC", "AC-04")]
    public void Register_ByAdministrator_Succeeds()
    {
        var connection = SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.ExternalJobSite, "Jobs4All",
            "https://jobs4all.example/api", Admin);

        connection.Enabled.Should().BeTrue();
        connection.Category.Should().Be(SoftwareInterfaceCategory.ExternalJobSite);
    }

    [Fact]
    [Trait("Story", "US-4.3-01")]
    [Trait("AC", "AC-04")]
    public void Register_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.EmailSmsGateway, "SMTP",
            "https://smtp.example", NonAdmin);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.SoftwareInterfaceAdminOnly);
        ex.ExternalCode.Should().Be(ErrorCodes.SoftwareInterfaceForbidden);
    }

    [Fact]
    public void SetEnabled_ByAdministrator_Toggles()
    {
        var connection = SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.AnalyticsReporting, "GA",
            "https://analytics.example", Admin);

        connection.SetEnabled(false, Admin);

        connection.Enabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateEndpoint_ByNonAdministrator_ThrowsForbidden()
    {
        var connection = SoftwareInterfaceConnection.Register(Guid.NewGuid(), SoftwareInterfaceCategory.GovernmentDatabase, "MoL",
            "https://mol.example", Admin);

        var act = () => connection.UpdateEndpoint("https://mol.example/v2", NonAdmin);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.SoftwareInterfaceAdminOnly);
    }
}
