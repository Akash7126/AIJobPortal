using System.Net;
using System.Net.Http.Json;
using JobPlatform.TestSupport;

namespace JobPlatform.JobPosting.Api.IntegrationTests;

public class PostingsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PostingsApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Create_AsEmployer_Returns201_AndTheLocationIsReadableWithAnETag()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.PostJsonAsync("/api/v1/jobs", ApiFactory.PostingBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var get = await client.GetAsync(response.Headers.Location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_Anonymous_Returns401()
    {
        var client = _factory.ClientFor(null);

        var response = await client.PostJsonAsync("/api/v1/jobs", ApiFactory.PostingBody());

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-UNAUTHORIZED");
    }

    [Fact]
    [Trait("Story", "US-4.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Create_AsJobSeeker_Returns403_JCPForbidden()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await client.PostJsonAsync("/api/v1/jobs", ApiFactory.PostingBody());

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-JCP-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-03")]
    public async Task Create_WithoutSkills_Returns400_ValidationError()
    {
        var client = _factory.ClientFor(TestTokens.Employer());
        var body = ApiFactory.PostingBody(skills: Array.Empty<string>());

        var response = await client.PostJsonAsync("/api/v1/jobs", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Create_SameContentTwice_ReturnsExistingDraft_NotDuplicated()
    {
        var employerClient = _factory.ClientFor(TestTokens.Employer(Guid.NewGuid()));
        var body = ApiFactory.PostingBody(titleEn: "Duplicate Check Role");

        var first = await employerClient.PostJsonAsync("/api/v1/jobs", body);
        var second = await employerClient.PostJsonAsync("/api/v1/jobs", body);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstId = (await first.Json())["jobPostingId"]!.GetValue<Guid>();
        var secondId = (await second.Json())["jobPostingId"]!.GetValue<Guid>();
        secondId.Should().Be(firstId);
    }

    [Fact]
    [Trait("Story", "US-4.1-02")]
    [Trait("AC", "AC-01")]
    public async Task Search_Anonymous_ReturnsOnlyActivePublicPostings()
    {
        var employerId = Guid.NewGuid();
        var employerClient = _factory.ClientFor(TestTokens.Employer(employerId));
        var created = await employerClient.PostJsonAsync("/api/v1/jobs", ApiFactory.PostingBody(titleEn: "Searchable Draft Role"));
        var postingId = (await created.Json())["jobPostingId"]!.GetValue<Guid>();
        var draftSearch = await _factory.ClientFor(null).GetAsync("/api/v1/jobs/search?keyword=Searchable Draft Role");
        (await draftSearch.Json())["totalCount"]!.GetValue<int>().Should().Be(0, "a draft posting must not appear in public search");

        var publish = await employerClient.PostAsJsonAsync($"/api/v1/jobs/{postingId}/status", new { status = "Active" });
        publish.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _factory.ClientFor(null).GetAsync("/api/v1/jobs/search?keyword=Searchable Draft Role");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["totalCount"]!.GetValue<int>().Should().Be(1);
        body["items"]![0]!["jobPostingId"]!.GetValue<Guid>().Should().Be(postingId);
    }

    [Fact]
    [Trait("Story", "US-3.2.2-03")]
    [Trait("AC", "AC-01")]
    public async Task ToggleFavorite_AsJobSeeker_TogglesOnAndOff()
    {
        var employerClient = _factory.ClientFor(TestTokens.Employer(Guid.NewGuid()));
        var created = await employerClient.PostJsonAsync("/api/v1/jobs", ApiFactory.PostingBody(titleEn: "Favourite Target Role"));
        var postingId = (await created.Json())["jobPostingId"]!.GetValue<Guid>();
        var jobSeekerClient = _factory.ClientFor(TestTokens.JobSeeker());

        var on = await jobSeekerClient.PutJsonAsync($"/api/v1/favorites/{postingId}");
        var off = await jobSeekerClient.PutJsonAsync($"/api/v1/favorites/{postingId}");

        (await on.Json())["favorited"]!.GetValue<bool>().Should().BeTrue();
        (await off.Json())["favorited"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task GetPostingForMatching_WithoutServiceToken_Returns401()
    {
        var response = await _factory.ClientFor(null).GetAsync($"/internal/v1/postings/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.3.1-01")]
    [Trait("AC", "AC-01")]
    public async Task GetPostingForMatching_WithServiceToken_ReturnsPosting()
    {
        var employerClient = _factory.ClientFor(TestTokens.Employer(Guid.NewGuid()));
        var created = await employerClient.PostJsonAsync("/api/v1/jobs", ApiFactory.PostingBody(titleEn: "Internal Lookup Role"));
        var postingId = (await created.Json())["jobPostingId"]!.GetValue<Guid>();

        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/postings/{postingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["title"]!.GetValue<string>().Should().Be("Internal Lookup Role");
    }
}
