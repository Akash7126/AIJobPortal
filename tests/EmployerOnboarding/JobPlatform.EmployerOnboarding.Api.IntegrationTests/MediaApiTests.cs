using System.Net;
using System.Net.Http.Headers;
using JobPlatform.TestSupport;

namespace JobPlatform.EmployerOnboarding.Api.IntegrationTests;

public class MediaApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MediaApiTests(ApiFactory factory) => _factory = factory;

    private static MultipartFormDataContent Form(string kind, byte[] bytes, string fileName = "logo.png", string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(kind), "kind");
        return content;
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-01")]
    public async Task Attach_ValidLogo_Returns201_AndPublishesTheEvent()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await employer.PostAsync("/api/v1/employers/me/media", Form("Logo", "hello"u8.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var list = await (await employer.GetAsync("/api/v1/employers/me/media")).Json();
        list.AsArray().Should().ContainSingle();

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "company-media-and-document.created.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-03")]
    public async Task Attach_SameFileTwice_ReturnsExistingWithoutDuplicating()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));
        var bytes = "duplicate-me"u8.ToArray();

        var first = await employer.PostAsync("/api/v1/employers/me/media", Form("Logo", bytes));
        var second = await employer.PostAsync("/api/v1/employers/me/media", Form("Logo", bytes));

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await first.Json())["companyMediaId"]!.GetValue<Guid>().Should().Be((await second.Json())["companyMediaId"]!.GetValue<Guid>());
        (await (await employer.GetAsync("/api/v1/employers/me/media")).Json()).AsArray().Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-02")]
    public async Task Attach_UnsupportedFormat_Is422()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));

        var response = await _factory.ClientFor(TestTokens.Employer(employerId)).PostAsync("/api/v1/employers/me/media",
            Form("Logo", "x"u8.ToArray(), "virus.exe", "application/x-msdownload"));

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-ERPM-UNSUPPORTED-FORMAT");
    }

    [Fact]
    public async Task Attach_ByAnotherEmployer_CannotRemoveIt()
    {
        var owner = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(owner));
        var uploaded = await (await _factory.ClientFor(TestTokens.Employer(owner)).PostAsync("/api/v1/employers/me/media", Form("Logo", "x"u8.ToArray()))).Json();
        var mediaId = uploaded["companyMediaId"]!.GetValue<Guid>();

        var response = await _factory.ClientFor(TestTokens.Employer()).DeleteAsync($"/api/v1/employers/me/media/{mediaId}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-ERPM-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    public async Task SetPrimary_SwapsThePreviousPrimary()
    {
        var employerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.EmployerAccountApproved(employerId));
        var employer = _factory.ClientFor(TestTokens.Employer(employerId));
        var first = (await (await employer.PostAsync("/api/v1/employers/me/media", Form("Logo", "one"u8.ToArray()))).Json())["companyMediaId"]!.GetValue<Guid>();
        var second = (await (await employer.PostAsync("/api/v1/employers/me/media", Form("Logo", "two"u8.ToArray()))).Json())["companyMediaId"]!.GetValue<Guid>();
        await employer.PutJsonAsync($"/api/v1/employers/me/media/{first}/primary");

        var response = await employer.PutJsonAsync($"/api/v1/employers/me/media/{second}/primary");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = (await (await employer.GetAsync("/api/v1/employers/me/media")).Json()).AsArray();
        list.Single(m => m!["companyMediaId"]!.GetValue<Guid>() == second)!["isPrimaryLogo"]!.GetValue<bool>().Should().BeTrue();
        list.Single(m => m!["companyMediaId"]!.GetValue<Guid>() == first)!["isPrimaryLogo"]!.GetValue<bool>().Should().BeFalse();
    }
}
