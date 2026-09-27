using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.GovernmentIntegration.Api.IntegrationTests;

public class InternalApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InternalApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-01")]
    public async Task RequestGovernmentVerification_AuthorisedComponent_Returns202AndStatusIsQueryable()
    {
        var service = _factory.ClientFor(TestTokens.Service());
        var subjectId = Guid.NewGuid();

        var response = await service.PostJsonAsync("/internal/v1/gov-verifications", new
        {
            requestingComponent = "employer-onboarding", subjectType = "Employer", subjectId, source = "MoL", purpose = "EmployerVerification"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var status = await (await service.GetAsync($"/internal/v1/verification-status?subjectType=Employer&subjectId={subjectId}")).Json();
        status["governmentDataStatus"]!.GetValue<string>().Should().Be("Verified");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    [Trait("AC", "AC-02")]
    public async Task RequestGovernmentVerification_UnauthorisedComponent_Is403WithForbiddenCode()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).PostJsonAsync("/internal/v1/gov-verifications", new
        {
            requestingComponent = "not-on-the-allow-list", subjectType = "Employer", subjectId = Guid.NewGuid(), source = "MoL",
            purpose = "EmployerVerification"
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-GDI-FORBIDDEN");
    }

    [Fact]
    public async Task InternalEndpoints_WithoutServiceToken_Are403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).GetAsync("/internal/v1/government-systems");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-GDI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "F-0001")]
    public async Task GetGovernmentSystems_ReturnsConfiguredConnections()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        await admin.PutJsonAsync("/api/v1/admin/government-connections/GovernmentDatabase",
            new { endpoint = "https://govdb.example/api", authMethod = "ApiKey", credentialRef = "ref", enabled = true });

        var service = _factory.ClientFor(TestTokens.Service());
        var systems = await (await service.GetAsync("/internal/v1/government-systems")).Json();

        systems.AsArray().Should().Contain(s => s!["source"]!.GetValue<string>() == "GovernmentDatabase");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-05")]
    public async Task GetGovernmentExchanges_ReturnsLoggedAccessDecisions()
    {
        var service = _factory.ClientFor(TestTokens.Service());
        await service.PostJsonAsync("/internal/v1/educational-verifications", new
        {
            requestingComponent = "job-seeker-profile", subjectId = Guid.NewGuid(), institution = "Birzeit University", credentialName = "BSc CS",
            year = 2020
        });

        var exchanges = await (await service.GetAsync("/internal/v1/government-exchanges")).Json();

        exchanges["items"]!.AsArray().Should().Contain(i => i!["component"]!.GetValue<string>() == "job-seeker-profile");
    }
}
