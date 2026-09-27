using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain.UnitTests;

public class ApiVersionTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);
    private static readonly Actor NonAdmin = new(Guid.NewGuid(), false);

    [Fact]
    [Trait("Story", "US-3.4.3-01")]
    public void Release_StartsActiveWithJsonOnly()
    {
        var version = ApiVersion.Release("v1");

        version.Status.Should().Be(ApiVersionStatus.Active);
        version.AcceptedFormats.Should().BeEquivalentTo(new[] { "json" });
    }

    [Fact]
    [Trait("Story", "US-3.4.3-05")]
    [Trait("AC", "AC-01")]
    public void Deprecate_WhileActive_Succeeds()
    {
        var version = ApiVersion.Release("v1");

        version.Deprecate(At.AddDays(90), At, Admin);

        version.Status.Should().Be(ApiVersionStatus.Deprecated);
        version.SunsetAtUtc.Should().Be(At.AddDays(90));
    }

    [Fact]
    public void Deprecate_ByNonAdministrator_ThrowsForbidden()
    {
        var version = ApiVersion.Release("v1");

        var act = () => version.Deprecate(At.AddDays(90), At, NonAdmin);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ApiAdminOnly);
        ex.ExternalCode.Should().Be(ErrorCodes.ApiAdminOnly);
    }

    [Fact]
    public void Deprecate_WithPastSunsetDate_ThrowsDeprecationWindowRequired()
    {
        var version = ApiVersion.Release("v1");

        var act = () => version.Deprecate(At.AddDays(-1), At, Admin);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ApiDeprecationWindowRequired);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-05")]
    [Trait("AC", "AC-04")]
    public void Retire_AfterSunset_Succeeds()
    {
        var version = ApiVersion.Release("v1");
        version.Deprecate(At.AddDays(90), At, Admin);

        version.Retire(Admin, At.AddDays(91));

        version.Status.Should().Be(ApiVersionStatus.Retired);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-05")]
    [Trait("AC", "AC-04")]
    public void Retire_BeforeSunset_ThrowsRetireBeforeSunset()
    {
        var version = ApiVersion.Release("v1");
        version.Deprecate(At.AddDays(90), At, Admin);

        var act = () => version.Retire(Admin, At.AddDays(1));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ApiRetireBeforeSunset);
        ex.ExternalCode.Should().Be(ErrorCodes.ApiRetireBeforeSunset);
    }

    [Fact]
    public void Retire_WhileActive_ThrowsNotDeprecated()
    {
        var version = ApiVersion.Release("v1");

        var act = () => version.Retire(Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ApiNotDeprecated);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-02")]
    [Trait("AC", "AC-03")]
    public void ConfigureAcceptedFormats_WithSupportedFormats_Succeeds()
    {
        var version = ApiVersion.Release("v1");

        version.ConfigureAcceptedFormats(new[] { "json", "xml" }, Admin);

        version.AcceptedFormats.Should().BeEquivalentTo(new[] { "json", "xml" });
    }

    [Fact]
    [Trait("Story", "US-3.4.3-02")]
    [Trait("AC", "AC-04")]
    public void ConfigureAcceptedFormats_WithUnsupportedFormat_ThrowsUnsupportedFormat()
    {
        var version = ApiVersion.Release("v1");

        var act = () => version.ConfigureAcceptedFormats(new[] { "soap" }, Admin);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ApiUnsupportedFormat);
        ex.ExternalCode.Should().Be(ErrorCodes.ApiUnsupportedFormat);
    }
}
