using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.JobSeekerProfile.Api.IntegrationTests;

public class ShareLinkApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ShareLinkApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientWithSharingActivatedAsync()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());
        await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());
        await client.PutJsonAsync("/api/v1/profiles/me/privacy/visibility", new { @public = true, publicSharingActive = true });
        return client;
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    public async Task Create_WithSharingActivated_Returns201_WithAUrl()
    {
        var client = await ClientWithSharingActivatedAsync();

        var response = await client.PostJsonAsync("/api/v1/profiles/me/share-link");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["url"]!.GetValue<string>().Should().Contain("/shared/");
        body["isActive"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    [Trait("AC", "AC-03")]
    public async Task Create_Twice_ReturnsTheSameLink()
    {
        var client = await ClientWithSharingActivatedAsync();

        var first = await (await client.PostJsonAsync("/api/v1/profiles/me/share-link")).Json();
        var second = await (await client.PostJsonAsync("/api/v1/profiles/me/share-link")).Json();

        second["token"]!.GetValue<string>().Should().Be(first["token"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    [Trait("AC", "AC-10")]
    public async Task Create_WithoutSharingActivated_Returns422()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());
        await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        var response = await client.PostJsonAsync("/api/v1/profiles/me/share-link");

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-JSRPM-SHARING-NOT-ACTIVATED");
    }

    [Fact]
    public async Task GetMine_WithoutOne_Returns404()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());
        await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        var response = await client.GetAsync("/api/v1/profiles/me/share-link");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    [Trait("AC", "AC-04")]
    public async Task SharedProfile_AnonymousRead_ByToken_ReturnsTheProfile()
    {
        var client = await ClientWithSharingActivatedAsync();
        var created = await (await client.PostJsonAsync("/api/v1/profiles/me/share-link")).Json();
        var token = created["token"]!.GetValue<string>();

        var response = await _factory.ClientFor(null).GetAsync($"/shared/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["fullName"]!.GetValue<string>().Should().Be("Layla Haddad");
    }

    [Fact]
    public async Task SharedProfile_UnknownToken_Returns404()
    {
        var response = await _factory.ClientFor(null).GetAsync("/shared/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    public async Task SharedProfile_Qr_ReturnsAnSvgImage()
    {
        var client = await ClientWithSharingActivatedAsync();
        var created = await (await client.PostJsonAsync("/api/v1/profiles/me/share-link")).Json();
        var token = created["token"]!.GetValue<string>();

        var response = await _factory.ClientFor(null).GetAsync($"/shared/{token}/qr");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        (await response.Content.ReadAsStringAsync()).Should().Contain("<svg");
    }
}
