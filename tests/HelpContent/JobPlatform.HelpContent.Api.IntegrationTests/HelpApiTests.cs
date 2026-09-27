using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.HelpContent.Api.IntegrationTests;

public class HelpApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HelpApiTests(ApiFactory factory) => _factory = factory;

    private static object HelpBody(string titleEn, string bodyEn = "Body", string kind = "Faq") =>
        new { kind, titleAr = (string?)null, titleEn, bodyAr = (string?)null, bodyEn };

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-01")]
    public async Task Create_ThenUpdate_CreatesNewVersion_AndPublishesTheEvent()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var create = await admin.PostJsonAsync("/api/v1/admin/help", HelpBody($"Q {Guid.NewGuid():N}"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var contentId = (await create.Json())["helpContentId"]!.GetValue<Guid>();

        var update = await admin.PutJsonAsync($"/api/v1/admin/help/{contentId}", HelpBody("Updated Q", "Updated A"));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Json())["currentVersion"]!.GetValue<int>().Should().Be(2);

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "help-content.updated.v1");
    }

    [Fact]
    public async Task Create_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).PostJsonAsync("/api/v1/admin/help", HelpBody("Q"));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-FAQHC-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-01")]
    [Trait("AC", "AC-02")]
    public async Task Search_NoResults_Returns200WithEmptyItems()
    {
        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/help/search?q={Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["items"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    [Trait("AC", "AC-02")]
    public async Task Context_UnmappedPageKey_Returns204_NotAnError()
    {
        // ASP.NET Core turns a 200 Ok(null) into 204 automatically; the client falls back to the general help center client-side.
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/help/context?pageKey=unmapped/page");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    public async Task ContextMapping_SetThenGet_ResolvesTheMappedContent()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var contentId = (await (await admin.PostJsonAsync("/api/v1/admin/help", HelpBody($"Q {Guid.NewGuid():N}"))).Json())["helpContentId"]!.GetValue<Guid>();
        var pageKey = $"employer/dashboard-{Guid.NewGuid():N}";

        var set = await admin.PutJsonAsync($"/api/v1/admin/help/context-mappings/{pageKey}", contentId);
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/help/context?pageKey={pageKey}");
        (await response.Json())["helpContentId"]!.GetValue<Guid>().Should().Be(contentId);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-08")]
    [Trait("AC", "AC-02")]
    public async Task AttachMedia_WithoutCaptions_Is400_CaughtByValidation()
    {
        // AttachHelpMediaValidator (400, before the domain is touched) catches this earlier than HelpContent.AttachMedia's own INV-10 guard
        // (422) would - see ValidatorTests and HelpContentTests.AttachMedia_WithoutCaptionsOrTextAlternative_ThrowsNoCaptions for each layer.
        var admin = _factory.ClientFor(TestTokens.Admin());
        var contentId = (await (await admin.PostJsonAsync("/api/v1/admin/help", HelpBody($"Q {Guid.NewGuid():N}", kind: "Video"))).Json())["helpContentId"]!.GetValue<Guid>();
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[10]) { Headers = { ContentType = new("video/mp4") } }, "file", "v.mp4" },
            { new StringContent("Video"), "type" }
        };

        var response = await admin.PostAsync($"/api/v1/admin/help/{contentId}/media", form);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-01")]
    public async Task SubmitFeedback_ThenSummary_IsAdminOnly()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var contentId = (await (await admin.PostJsonAsync("/api/v1/admin/help", HelpBody($"Q {Guid.NewGuid():N}"))).Json())["helpContentId"]!.GetValue<Guid>();
        var jobSeeker = _factory.ClientFor(TestTokens.JobSeeker());

        var submit = await jobSeeker.PostJsonAsync($"/api/v1/help/{contentId}/feedback", new { rating = "Helpful", comment = "Useful" });
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await admin.GetAsync($"/api/v1/admin/help/feedback/summary?helpContentId={contentId}");
        summary.StatusCode.Should().Be(HttpStatusCode.OK);
        (await summary.Json())["helpfulCount"]!.GetValue<int>().Should().Be(1);

        var forbidden = await jobSeeker.GetAsync($"/api/v1/admin/help/feedback/summary?helpContentId={contentId}");
        await forbidden.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-FAQHC-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-02")]
    public async Task SubmitFeedback_SameUserTwice_ReplacesWithoutDuplicating()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var contentId = (await (await admin.PostJsonAsync("/api/v1/admin/help", HelpBody($"Q {Guid.NewGuid():N}"))).Json())["helpContentId"]!.GetValue<Guid>();
        var jobSeeker = _factory.ClientFor(TestTokens.JobSeeker(Guid.NewGuid()));

        await jobSeeker.PostJsonAsync($"/api/v1/help/{contentId}/feedback", new { rating = "Helpful", comment = "First" });
        await jobSeeker.PostJsonAsync($"/api/v1/help/{contentId}/feedback", new { rating = "NotHelpful", comment = "Changed" });

        var summary = await admin.GetAsync($"/api/v1/admin/help/feedback/summary?helpContentId={contentId}");
        var body = await summary.Json();
        body["helpfulCount"]!.GetValue<int>().Should().Be(0);
        body["notHelpfulCount"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    public async Task Organization_AssignThenHelpCenter_FiltersByRole()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var contentId = (await (await admin.PostJsonAsync("/api/v1/admin/help", HelpBody($"EmployerOnly {Guid.NewGuid():N}"))).Json())["helpContentId"]!.GetValue<Guid>();

        var assign = await admin.PutJsonAsync($"/api/v1/admin/help/{contentId}/organization", new { topicId = (Guid?)null, roles = new[] { "Employer" } });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var jobSeekerView = await (await _factory.ClientFor(null).GetAsync("/api/v1/help?role=JobSeeker")).Json();
        var employerView = await (await _factory.ClientFor(null).GetAsync("/api/v1/help?role=Employer")).Json();

        jobSeekerView.AsArray().SelectMany(t => t!["items"]!.AsArray()).Should().NotContain(i => i!["helpContentId"]!.GetValue<Guid>() == contentId);
        employerView.AsArray().SelectMany(t => t!["items"]!.AsArray()).Should().Contain(i => i!["helpContentId"]!.GetValue<Guid>() == contentId);
    }
}

public class TutorialsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TutorialsApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.7.2-07")]
    [Trait("AC", "AC-01")]
    public async Task Get_ThenComplete_ThenGetAgain_ReportsCompleted()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var tutorialId = (await (await admin.PostJsonAsync("/api/v1/admin/help", new { kind = "Guide", titleAr = (string?)null, titleEn = "Getting started", bodyAr = (string?)null, bodyEn = "Body" }))
            .Json())["helpContentId"]!.GetValue<Guid>();
        var jobSeeker = _factory.ClientFor(TestTokens.JobSeeker());

        var before = await (await jobSeeker.GetAsync($"/api/v1/tutorials/{tutorialId}")).Json();
        before["completed"]!.GetValue<bool>().Should().BeFalse();

        var complete = await jobSeeker.PostJsonAsync($"/api/v1/tutorials/{tutorialId}/complete");
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await jobSeeker.GetAsync($"/api/v1/tutorials/{tutorialId}")).Json();
        after["completed"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Get_Anonymous_Is401()
    {
        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/tutorials/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
