using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class CompanyProfilePageTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-04")]
    public void EditBackground_ByOwningEmployer_Succeeds()
    {
        var employerId = Guid.NewGuid();
        var page = CompanyProfilePage.OpenFor(Guid.NewGuid(), employerId, At);

        page.EditBackground(new LocalizedText(null, "We build great things."), new[] { "Great team" }, new Actor(employerId, false), At.AddMinutes(1));

        page.Background.En.Should().Be("We build great things.");
        page.Highlights.Should().ContainSingle();
        page.PublishedAtUtc.Should().Be(At.AddMinutes(1));
    }

    [Fact]
    [Trait("Story", "US-3.1.2-05")]
    [Trait("AC", "AC-04")]
    public void EditBackground_ByAdministrator_Succeeds()
    {
        var page = CompanyProfilePage.OpenFor(Guid.NewGuid(), Guid.NewGuid(), At);

        page.EditBackground(new LocalizedText(null, "Edited by support"), Array.Empty<string>(), new Actor(Guid.NewGuid(), true), At);

        page.Background.En.Should().Be("Edited by support");
    }

    [Fact]
    public void EditBackground_ByUnrelatedActor_ThrowsForbidden()
    {
        var page = CompanyProfilePage.OpenFor(Guid.NewGuid(), Guid.NewGuid(), At);

        var act = () => page.EditBackground(new LocalizedText(null, "x"), Array.Empty<string>(), new Actor(Guid.NewGuid(), false), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.CompanyPageOwnerOrAdminOnly);
        ex.ExternalCode.Should().Be("E-HCCP-FORBIDDEN");
    }

    [Fact]
    public void EditBackground_TooManyHighlights_Throws()
    {
        var employerId = Guid.NewGuid();
        var page = CompanyProfilePage.OpenFor(Guid.NewGuid(), employerId, At);
        var highlights = Enumerable.Range(0, CompanyProfilePage.MaxHighlights + 1).Select(i => $"h{i}").ToArray();

        var act = () => page.EditBackground(new LocalizedText(null, "x"), highlights, new Actor(employerId, false), At);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
