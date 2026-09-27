using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.CandidateSourcing.Api.IntegrationTests;

public class TalentPoolApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TalentPoolApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    public async Task Add_ThenList_ShowsTheEntry()
    {
        var client = _factory.ClientFor(TestTokens.Employer());
        var candidateId = await _factory.PublicCandidateAsync();
        var jobPostingId = Guid.NewGuid();

        var response = await client.PostJsonAsync("/api/v1/employers/me/talent-pool", new { candidateProfileId = candidateId, jobPostingId, note = "Strong candidate" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var list = await (await client.GetAsync("/api/v1/employers/me/talent-pool")).Json();
        list.AsArray().Should().ContainSingle(e => e!["candidateProfileId"]!.GetValue<Guid>() == candidateId);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-02")]
    public async Task Add_SamePairTwice_ReturnsTheExistingEntry()
    {
        var client = _factory.ClientFor(TestTokens.Employer());
        var candidateId = await _factory.PublicCandidateAsync();
        var body = new { candidateProfileId = candidateId, jobPostingId = Guid.NewGuid(), note = (string?)null };

        var first = await client.PostJsonAsync("/api/v1/employers/me/talent-pool", body);
        var second = await client.PostJsonAsync("/api/v1/employers/me/talent-pool", body);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await first.Json())["talentPoolEntryId"]!.GetValue<Guid>().Should().Be((await second.Json())["talentPoolEntryId"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Add_WithTooLongNote_Returns400()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.PostJsonAsync("/api/v1/employers/me/talent-pool",
            new { candidateProfileId = Guid.NewGuid(), jobPostingId = Guid.NewGuid(), note = new string('x', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    public async Task Remove_ThenList_NoLongerShowsIt()
    {
        var client = _factory.ClientFor(TestTokens.Employer());
        var candidateId = await _factory.PublicCandidateAsync();
        var created = await (await client.PostJsonAsync("/api/v1/employers/me/talent-pool",
            new { candidateProfileId = candidateId, jobPostingId = Guid.NewGuid(), note = (string?)null })).Json();
        var entryId = created["talentPoolEntryId"]!.GetValue<Guid>();

        var response = await client.DeleteAsync($"/api/v1/employers/me/talent-pool/{entryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await (await client.GetAsync("/api/v1/employers/me/talent-pool")).Json();
        list.AsArray().Should().NotContain(e => e!["talentPoolEntryId"]!.GetValue<Guid>() == entryId);
    }

    [Fact]
    public async Task Remove_UnknownEntry_Returns404()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.DeleteAsync($"/api/v1/employers/me/talent-pool/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-CRFE-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-04")]
    public async Task Add_ForACandidateNotVisibleToEmployers_Returns403()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.PostJsonAsync("/api/v1/employers/me/talent-pool",
            new { candidateProfileId = Guid.NewGuid(), jobPostingId = Guid.NewGuid(), note = (string?)null });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }

    [Fact]
    public async Task Add_AsJobSeeker_Returns403()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await client.PostJsonAsync("/api/v1/employers/me/talent-pool",
            new { candidateProfileId = Guid.NewGuid(), jobPostingId = Guid.NewGuid(), note = (string?)null });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }
}
