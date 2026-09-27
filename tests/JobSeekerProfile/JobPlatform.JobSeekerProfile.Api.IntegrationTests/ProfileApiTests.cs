using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.JobSeekerProfile.Api.IntegrationTests;

public class ProfileApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProfileApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientWithProfileAsync()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());
        await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());
        return client;
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    public async Task Create_AsJobSeeker_Returns201_AndIsReadableAtMe()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());

        var response = await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Json();
        body["fullName"]!.GetValue<string>().Should().Be("Layla Haddad");

        var get = await client.GetAsync("/api/v1/profiles/me");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_Anonymous_Returns401()
    {
        var response = await _factory.ClientFor(null).PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    public async Task Create_AsEmployer_Returns403_JsrpmForbidden()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-JSRPM-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    public async Task Create_WithoutAnActiveAccountRecord_Returns422()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-JSRPM-ACCOUNT-NOT-ACTIVE");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    public async Task Create_Twice_ForSameOwner_Returns409_Conflict()
    {
        var jobSeekerId = Guid.NewGuid();
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync(jobSeekerId));
        await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        var response = await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-JSRPM-DUPLICATE");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-01")]
    public async Task Create_WithInvalidEmail_Returns400()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());

        var response = await client.PostJsonAsync("/api/v1/profiles", new { fullName = "A B", email = "not-an-email", mobileNumber = "+970590000001", gender = "Female" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMine_WithoutAProfile_Returns404()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/profiles/me");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-JSRPM-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    public async Task UpdateLevel1_ThenGet_ReflectsTheChange()
    {
        var client = await ClientWithProfileAsync();

        var update = await client.PutJsonAsync("/api/v1/profiles/me/level1",
            new { fullName = "Layla Updated", email = "layla@example.org", mobileNumber = "+970590000001", gender = "Female" });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await client.GetAsync("/api/v1/profiles/me");
        (await get.Json())["fullName"]!.GetValue<string>().Should().Be("Layla Updated");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    [Trait("AC", "AC-03")]
    public async Task UpdateLevel1_WithStaleIfMatch_Returns409()
    {
        var client = await ClientWithProfileAsync();
        var staleTag = (await client.GetAsync("/api/v1/profiles/me")).Headers.ETag!.Tag;
        // Advance the row version so the stale tag no longer matches.
        await client.PutJsonAsync("/api/v1/profiles/me/level1",
            new { fullName = "First Update", email = "layla@example.org", mobileNumber = "+970590000001", gender = "Female" });

        var response = await client.PutJsonAsync("/api/v1/profiles/me/level1",
            new { fullName = "Second Update", email = "layla@example.org", mobileNumber = "+970590000001", gender = "Female" }, ifMatch: staleTag);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-JSRPM-CONFLICT");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    public async Task UpdateEducation_WithEmptyDegree_Returns400()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PutJsonAsync("/api/v1/profiles/me/education",
            new object[] { new { degree = "", institution = "Birzeit", from = (DateTime?)null, to = (DateTime?)null } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    public async Task UpdateSkills_ThenGetCompletion_IncludesTheSkillsWeight()
    {
        var client = await ClientWithProfileAsync();

        var update = await client.PutJsonAsync("/api/v1/profiles/me/skills",
            new object[] { new { name = "C#", kind = "Primary", @class = "Hard" } });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var completion = await client.GetAsync("/api/v1/profiles/me/completion");
        completion.StatusCode.Should().Be(HttpStatusCode.OK);
        (await completion.Json())["percent"]!.GetValue<int>().Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-06")]
    public async Task ConfigureJobPreference_ThenGet_RoundTrips()
    {
        var client = await ClientWithProfileAsync();

        var configure = await client.PutJsonAsync("/api/v1/profiles/me/job-preference", new
        {
            jobTypes = new[] { "FullTime" }, industries = new[] { "Technology" }, locations = new[] { "Ramallah" },
            salaryMin = (decimal?)1000, salaryMax = (decimal?)2000, salaryCurrency = "ILS", workArrangements = new[] { "Online" }
        });

        configure.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await client.GetAsync("/api/v1/profiles/me/job-preference");
        var body = await get.Json();
        body["jobTypes"]!.AsArray().Should().ContainSingle();
        body["locations"]!.AsArray()[0]!.GetValue<string>().Should().Be("Ramallah");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-06")]
    public async Task ConfigureJobPreference_WithInvertedSalaryRange_Returns400()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PutJsonAsync("/api/v1/profiles/me/job-preference", new
        {
            jobTypes = Array.Empty<string>(), industries = Array.Empty<string>(), locations = Array.Empty<string>(),
            salaryMin = (decimal?)5000, salaryMax = (decimal?)1000, salaryCurrency = (string?)null, workArrangements = Array.Empty<string>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    public async Task SetVisibility_ThenGetPrivacy_ReflectsPublic()
    {
        var client = await ClientWithProfileAsync();

        var setVisibility = await client.PutJsonAsync("/api/v1/profiles/me/privacy/visibility", new { @public = true, publicSharingActive = true });

        setVisibility.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await client.GetAsync("/api/v1/profiles/me/privacy");
        (await get.Json())["visibility"]!.GetValue<string>().Should().Be("Public");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    public async Task RequestDeactivation_Returns202()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostJsonAsync("/api/v1/profiles/me/privacy/deactivation", new { reason = "Taking a break" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    public async Task RequestDeletion_WithoutConfirm_Returns400()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostJsonAsync("/api/v1/profiles/me/privacy/deletion", new { confirm = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    public async Task RequestDeletion_WithConfirm_Returns202()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostJsonAsync("/api/v1/profiles/me/privacy/deletion", new { confirm = true });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
