using System.Net;
using System.Net.Http.Headers;
using JobPlatform.TestSupport;

namespace JobPlatform.EmployerOnboarding.Api.IntegrationTests;

public class InternalApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InternalApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Standing_UnknownEmployer_Is404()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/employers/{Guid.NewGuid()}/standing");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-EO-NOT-FOUND");
    }

    [Fact]
    public async Task Standing_WithoutServiceToken_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).GetAsync($"/internal/v1/employers/{Guid.NewGuid()}/standing");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Company_ReturnsIdentityAndLogo_OnceLevel2IsSubmitted()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));
        await employer.PutJsonAsync("/api/v1/employers/me/registration/level2", new
        {
            companyName = "Acme Ltd", companyId = "CO-9", registrationNumber = "REG-9", website = "https://acme.example", industry = "Software",
            size = "Small", governorate = "Ramallah", city = "Ramallah", street = (string?)null, description = "d"
        });

        var body = await (await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/employers/{employerId}/company")).Json();

        body["name"]!.GetValue<string>().Should().Be("Acme Ltd");
        body["industry"]!.GetValue<string>().Should().Be("Software");
    }

    [Fact]
    public async Task Company_UnknownEmployer_Is404()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/employers/{Guid.NewGuid()}/company");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
