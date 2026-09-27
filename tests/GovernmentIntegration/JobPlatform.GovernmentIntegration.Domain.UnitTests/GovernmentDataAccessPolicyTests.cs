using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class GovernmentDataAccessPolicyTests
{
    private readonly GovernmentDataAccessPolicy _policy = new();

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    [Trait("AC", "AC-01")]
    public void Authorise_AllowListedComponentAndPurpose_Allows()
    {
        var decision = _policy.Authorise(GovernmentDataAccessPolicy.EmployerOnboardingComponent, AccessPurpose.EmployerVerification);

        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    [Trait("AC", "AC-02")]
    public void Authorise_UnknownComponent_DeniesWithForbiddenCode()
    {
        var decision = _policy.Authorise("some-unknown-component", AccessPurpose.EmployerVerification);

        decision.IsAllowed.Should().BeFalse();
        decision.ErrorCode.Should().Be("E-GDI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    [Trait("AC", "AC-02")]
    public void Authorise_KnownComponentWrongPurpose_Denies()
    {
        var decision = _policy.Authorise(GovernmentDataAccessPolicy.EmployerOnboardingComponent, AccessPurpose.Migration);

        decision.IsAllowed.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    public void Authorise_JobSeekerProfileComponent_AllowsCredentialAndIdentityAndEnrichment()
    {
        _policy.Authorise(GovernmentDataAccessPolicy.JobSeekerProfileComponent, AccessPurpose.CredentialVerification).IsAllowed.Should().BeTrue();
        _policy.Authorise(GovernmentDataAccessPolicy.JobSeekerProfileComponent, AccessPurpose.IdentityVerification).IsAllowed.Should().BeTrue();
        _policy.Authorise(GovernmentDataAccessPolicy.JobSeekerProfileComponent, AccessPurpose.Enrichment).IsAllowed.Should().BeTrue();
        _policy.Authorise(GovernmentDataAccessPolicy.JobSeekerProfileComponent, AccessPurpose.EmployerVerification).IsAllowed.Should().BeFalse();
    }
}
