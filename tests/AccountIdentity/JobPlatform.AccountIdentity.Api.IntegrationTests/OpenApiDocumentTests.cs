using System.Net;
using System.Text.Json.Nodes;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>The published OpenAPI document (foundation section 11): bearer scheme, security on protected operations, problem+json responses.</summary>
public class OpenApiDocumentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OpenApiDocumentTests(ApiFactory factory) => _factory = factory;

    private async Task<JsonNode> DocumentAsync()
    {
        var response = await new ApiClient(_factory).Http.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Json();
    }

    [Fact]
    public async Task Document_DescribesTheServiceAndTheBearerScheme()
    {
        var document = await DocumentAsync();

        document["info"]!["title"]!.GetValue<string>().Should().Contain("Account Identity");
        var scheme = document["components"]!["securitySchemes"]!["Bearer"]!;
        scheme["type"]!.GetValue<string>().Should().Be("http");
        scheme["scheme"]!.GetValue<string>().Should().Be("bearer");
        scheme["bearerFormat"]!.GetValue<string>().Should().Be("JWT");
    }

    [Fact]
    public async Task ProtectedOperations_RequireTheBearerScheme_AndPublicOnesDoNot()
    {
        var paths = (await DocumentAsync())["paths"]!;

        paths["/api/v1/admin/accounts"]!["get"]!["security"].Should().NotBeNull();
        paths["/api/v1/admin/accounts"]!["get"]!["responses"]!["403"].Should().NotBeNull();
        paths["/api/v1/auth/logout"]!["post"]!["security"].Should().NotBeNull();
        paths["/api/v1/accounts/job-seekers"]!["post"]!["security"].Should().BeNull("registration is anonymous");
    }

    [Fact]
    public async Task CommandOperations_DocumentTheProblemResponses()
    {
        var paths = (await DocumentAsync())["paths"]!;
        var register = paths["/api/v1/accounts/job-seekers"]!["post"]!["responses"]!;

        foreach (var status in new[] { "400", "409", "422", "429" })
        {
            register[status]!["content"]!["application/problem+json"].Should().NotBeNull($"{status} is documented as problem+json");
        }

        paths["/api/v1/admin/password-policy"]!["put"]!["responses"]!["412"].Should().NotBeNull();
        paths["/api/v1/admin/accounts"]!["get"]!["responses"]!["412"].Should().BeNull("only updates take If-Match");
    }
}
