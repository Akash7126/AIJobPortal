using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json.Nodes;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>Authorization-server metadata: other BCs discover the issuer, the JWKS and the token endpoint from one anonymous document.</summary>
public class DiscoveryApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DiscoveryApiTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/oauth-authorization-server")]
    public async Task Discovery_IsAnonymous_AndPointsAtTheJwksAndTheTokenEndpoint(string path)
    {
        var client = new ApiClient(_factory);

        var response = await client.Http.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await response.Json();
        document["jwks_uri"]!.GetValue<string>().Should().EndWith("/.well-known/jwks.json");
        document["token_endpoint"]!.GetValue<string>().Should().EndWith("/oauth/token");
        document["grant_types_supported"]!.AsArray().Select(g => g!.GetValue<string>()).Should().Contain("client_credentials");
        document["id_token_signing_alg_values_supported"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Contain("RS256");
    }

    [Fact]
    public async Task Discovery_IssuerEqualsTheIssuerOfTheTokensAndItsJwksUriServesTheSigningKeys()
    {
        var client = new ApiClient(_factory);
        var document = await (await client.Http.GetAsync("/.well-known/openid-configuration")).Json();
        var service = await client.ServiceAsync();
        var token = service.Http.DefaultRequestHeaders.Authorization!.Parameter!;

        new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer.Should().Be(document["issuer"]!.GetValue<string>());
        var jwks = JsonNode.Parse(await client.Http.GetStringAsync(new Uri(document["jwks_uri"]!.GetValue<string>()).AbsolutePath))!;
        jwks["keys"]!.AsArray().Should().NotBeEmpty();
    }
}
