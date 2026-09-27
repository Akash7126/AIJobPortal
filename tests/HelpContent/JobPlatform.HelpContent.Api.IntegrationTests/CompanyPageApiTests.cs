using System.Net;
using JobPlatform.HelpContent.Application;
using JobPlatform.TestSupport;

namespace JobPlatform.HelpContent.Api.IntegrationTests;

public class CompanyPageApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CompanyPageApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-01")]
    public async Task Get_EmployerNotRegisteredWithBC05_Is404()
    {
        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/companies/{Guid.NewGuid()}/page");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-02")]
    public async Task Get_UnverifiedEmployer_ShowsNoBadge()
    {
        var employerId = Guid.NewGuid();
        _factory.CompanyDirectory.Register(employerId, new CompanyDirectoryEntry("Acme Ltd", null, "Software", "Small", "https://acme.example", false, null));

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/companies/{employerId}/page");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["verified"]!.GetValue<bool>().Should().BeFalse();
        body["badge"].Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-04")]
    public async Task Get_VerifiedEmployer_WithOpenPostingsDegraded_ShowsBadgeAndEmptyPostings()
    {
        var employerId = Guid.NewGuid();
        _factory.CompanyDirectory.Register(employerId, new CompanyDirectoryEntry("Acme Ltd", null, "Software", "Small", "https://acme.example", true, "Verified Employer"));
        _factory.OpenPostings.Degraded = true;

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/companies/{employerId}/page");

        var body = await response.Json();
        body["verified"]!.GetValue<bool>().Should().BeTrue();
        body["badge"]!.GetValue<string>().Should().Be("Verified Employer");
        body["openPostingsDegraded"]!.GetValue<bool>().Should().BeTrue();
        body["openPostings"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-04")]
    public async Task EditMine_ByOwningEmployer_UpdatesTheBackground()
    {
        var employerId = Guid.NewGuid();
        _factory.CompanyDirectory.Register(employerId, new CompanyDirectoryEntry("Acme Ltd", null, "Software", "Small", "https://acme.example", true, "Verified Employer"));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));

        var edit = await employer.PutJsonAsync("/api/v1/companies/me/page", new { backgroundAr = (string?)null, backgroundEn = "We build great things.", highlights = new[] { "Great team" } });

        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var page = await (await _factory.ClientFor(null).GetAsync($"/api/v1/companies/{employerId}/page")).Json();
        page["background"]!["en"]!.GetValue<string>().Should().Be("We build great things.");
    }

    [Fact]
    public async Task EditMine_ByJobSeeker_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker())
            .PutJsonAsync("/api/v1/companies/me/page", new { backgroundAr = (string?)null, backgroundEn = "x", highlights = Array.Empty<string>() });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-HCCP-FORBIDDEN");
    }
}
