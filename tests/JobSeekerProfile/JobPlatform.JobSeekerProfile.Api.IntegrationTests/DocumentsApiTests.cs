using System.Net;
using System.Net.Http.Headers;
using JobPlatform.TestSupport;

namespace JobPlatform.JobSeekerProfile.Api.IntegrationTests;

public class DocumentsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DocumentsApiTests(ApiFactory factory) => _factory = factory;

    private static MultipartFormDataContent DocumentForm(byte[] bytes, string documentType = "Certificate", string fileName = "cert.pdf",
        string contentType = "application/pdf")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(documentType), "documentType");
        return content;
    }

    private static MultipartFormDataContent ResumeForm(byte[] bytes, string fileName = "resume.pdf", string contentType = "application/pdf")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private async Task<HttpClient> ClientWithProfileAsync()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());
        await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody());
        return client;
    }

    [Fact]
    [Trait("Story", "US-3.1.1-09")]
    public async Task AttachDocument_ValidPdf_Returns201_AndIsListed()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostAsync("/api/v1/profiles/me/documents", DocumentForm("hello"u8.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var list = await (await client.GetAsync("/api/v1/profiles/me/documents")).Json();
        list.AsArray().Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-09")]
    public async Task AttachDocument_UnsupportedFormat_Returns400()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostAsync("/api/v1/profiles/me/documents",
            DocumentForm("x"u8.ToArray(), fileName: "virus.exe", contentType: "application/x-msdownload"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-JSRPM-UNSUPPORTED-FORMAT");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-09")]
    public async Task AttachDocument_SameContentTwice_ReturnsExistingWithoutDuplicating()
    {
        var client = await ClientWithProfileAsync();
        var bytes = "duplicate-me"u8.ToArray();

        var first = await client.PostAsync("/api/v1/profiles/me/documents", DocumentForm(bytes));
        var second = await client.PostAsync("/api/v1/profiles/me/documents", DocumentForm(bytes));

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await first.Json())["documentId"]!.GetValue<Guid>().Should().Be((await second.Json())["documentId"]!.GetValue<Guid>());
        (await (await client.GetAsync("/api/v1/profiles/me/documents")).Json()).AsArray().Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-09")]
    public async Task RemoveDocument_AsOwner_Returns204()
    {
        var client = await ClientWithProfileAsync();
        var uploaded = await (await client.PostAsync("/api/v1/profiles/me/documents", DocumentForm("removable"u8.ToArray()))).Json();
        var documentId = uploaded["documentId"]!.GetValue<Guid>();

        var response = await client.DeleteAsync($"/api/v1/profiles/me/documents/{documentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveDocument_ThatDoesNotExist_Returns404()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.DeleteAsync($"/api/v1/profiles/me/documents/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-JSRPM-NOT-FOUND");
    }

    [Fact]
    public async Task RemoveDocument_ByAnotherJobSeeker_Returns403()
    {
        var owner = await ClientWithProfileAsync();
        var uploaded = await (await owner.PostAsync("/api/v1/profiles/me/documents", DocumentForm("mine"u8.ToArray()))).Json();
        var documentId = uploaded["documentId"]!.GetValue<Guid>();
        var stranger = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await stranger.DeleteAsync($"/api/v1/profiles/me/documents/{documentId}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-JSRPM-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    public async Task UploadResume_ValidPdf_Returns201_AndIsReadableAsMetadata()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostAsync("/api/v1/profiles/me/resume", ResumeForm("resume-bytes"u8.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var get = await client.GetAsync("/api/v1/profiles/me/resume");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Json())["format"]!.GetValue<string>().Should().Be("Pdf");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    public async Task UploadResume_UnsupportedFormat_Returns400()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.PostAsync("/api/v1/profiles/me/resume",
            ResumeForm("x"u8.ToArray(), fileName: "photo.png", contentType: "image/png"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-JSRPM-UNSUPPORTED-FORMAT");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    [Trait("AC", "AC-04")]
    public async Task UploadResume_TwiceReplacesThePrevious()
    {
        var client = await ClientWithProfileAsync();
        await client.PostAsync("/api/v1/profiles/me/resume", ResumeForm("first"u8.ToArray()));

        var second = await client.PostAsync("/api/v1/profiles/me/resume", ResumeForm("second"u8.ToArray(), fileName: "resume2.pdf"));

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var get = await client.GetAsync("/api/v1/profiles/me/resume");
        (await get.Json())["fileName"]!.GetValue<string>().Should().Be("resume2.pdf");
    }

    [Fact]
    public async Task GetResume_WithoutOne_Returns404()
    {
        var client = await ClientWithProfileAsync();

        var response = await client.GetAsync("/api/v1/profiles/me/resume");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AttachDocument_Anonymous_Returns401()
    {
        var response = await _factory.ClientFor(null).PostAsync("/api/v1/profiles/me/documents", DocumentForm("x"u8.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
