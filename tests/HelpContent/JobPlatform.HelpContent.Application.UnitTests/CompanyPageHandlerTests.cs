using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application.UnitTests;

public class CompanyPageHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-01")]
    public async Task GetCompanyProfilePageHandler_EmployerNotRegistered_ReturnsNotFound()
    {
        var handler = new GetCompanyProfilePageHandler(new FakeCompanyDirectoryProvider(), new FakeOpenPostingsProvider(), new FakeStore(), new FakeHelpContentCache());

        var result = await handler.Handle(new GetCompanyProfilePageQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-02")]
    public async Task GetCompanyProfilePageHandler_UnverifiedEmployer_ShowsNoBadge()
    {
        var directory = new FakeCompanyDirectoryProvider { Entry = new CompanyDirectoryEntry("Acme", null, "Software", "Small", "https://acme.example", false, null) };
        var handler = new GetCompanyProfilePageHandler(directory, new FakeOpenPostingsProvider(), new FakeStore(), new FakeHelpContentCache());

        var result = await handler.Handle(new GetCompanyProfilePageQuery(Guid.NewGuid()), CancellationToken.None);

        result.Value.Verified.Should().BeFalse();
        result.Value.Badge.Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-04")]
    public async Task GetCompanyProfilePageHandler_OpenPostingsProviderDegraded_ReturnsEmptyListWithFlag()
    {
        var directory = new FakeCompanyDirectoryProvider { Entry = new CompanyDirectoryEntry("Acme", null, "Software", "Small", "https://acme.example", true, "Verified Employer") };
        var openPostings = new FakeOpenPostingsProvider { Degraded = true };
        var handler = new GetCompanyProfilePageHandler(directory, openPostings, new FakeStore(), new FakeHelpContentCache());

        var result = await handler.Handle(new GetCompanyProfilePageQuery(Guid.NewGuid()), CancellationToken.None);

        result.Value.OpenPostingsDegraded.Should().BeTrue();
        result.Value.OpenPostings.Should().BeEmpty();
        result.Value.Verified.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    public async Task GetCompanyProfilePageHandler_UsesCache_WhenPresent()
    {
        var cache = new FakeHelpContentCache();
        var employerId = Guid.NewGuid();
        var cached = new CompanyPageView(employerId, "Cached", null, "Software", "Small", "https://acme.example", true, "Verified Employer",
            new(null, null), Array.Empty<string>(), Array.Empty<OpenPostingView>(), false);
        await cache.SetCompanyPageAsync(employerId, cached, CancellationToken.None);
        var handler = new GetCompanyProfilePageHandler(new FakeCompanyDirectoryProvider(), new FakeOpenPostingsProvider(), new FakeStore(), cache);

        var result = await handler.Handle(new GetCompanyProfilePageQuery(employerId), CancellationToken.None);

        result.Value.Name.Should().Be("Cached");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    public async Task EditCompanyPageHandler_FirstEdit_CreatesPage()
    {
        var store = new FakeStore();
        var employerId = Guid.NewGuid();
        var handler = new EditCompanyPageHandler(store, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(new EditCompanyPageCommand(null, "We build things.", new[] { "Great culture" }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.CompanyPages.Should().ContainSingle().Which.Background.En.Should().Be("We build things.");
    }
}
