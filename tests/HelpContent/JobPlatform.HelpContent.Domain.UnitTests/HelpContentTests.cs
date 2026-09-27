using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class HelpContentTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static LocalizedText Text(string value) => new(null, value);

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-01")]
    public void Create_Valid_StartsAtVersionOne()
    {
        var content = HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, Text("Q"), Text("A"), Admin, At);

        content.CurrentVersion.Should().Be(1);
        content.Versions.Should().ContainSingle();
        content.DomainEvents.Should().BeEmpty("creation is not catalogued as HelpContentUpdated");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-02")]
    public void Create_MissingFields_ThrowsRequiredField()
    {
        var act = () => HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, Text(""), Text(""), Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.HelpRequiredField);
        ex.ExternalCode.Should().Be("E-FAQHC-REQUIRED-FIELD");
    }

    [Fact]
    public void Create_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, Text("Q"), Text("A"), new Actor(Guid.NewGuid(), false), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.HelpAdminOnly);
        ex.ExternalCode.Should().Be("E-FAQHC-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-01")]
    public void Update_AppendsNewVersionAtomically_AndRaisesEvent()
    {
        var content = HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, Text("Q1"), Text("A1"), Admin, At);

        content.Update(Text("Q2"), Text("A2"), Admin, At.AddMinutes(5));

        content.CurrentVersion.Should().Be(2);
        content.Versions.Should().HaveCount(2, "every save is retained as a version");
        content.Current.Title.En.Should().Be("Q2");
        content.Versions.Single(v => v.VersionNo == 1).Title.En.Should().Be("Q1", "a prior version is never mutated");
        var evt = content.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<HelpContentUpdatedDomainEvent>().Which;
        evt.FromVersion.Should().Be(1);
        evt.ToVersion.Should().Be(2);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-03")]
    public void Update_Repeatedly_LaterSaveWins_ButEveryVersionRetained()
    {
        var content = HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, Text("Q1"), Text("A1"), Admin, At);

        content.Update(Text("Q2"), Text("A2"), Admin, At.AddMinutes(1));
        content.Update(Text("Q3"), Text("A3"), Admin, At.AddMinutes(2));

        content.CurrentVersion.Should().Be(3);
        content.Versions.Should().HaveCount(3);
        content.Current.Title.En.Should().Be("Q3");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-08")]
    [Trait("AC", "AC-02")]
    public void AttachMedia_WithoutCaptionsOrTextAlternative_ThrowsNoCaptions()
    {
        var content = HelpContent.Create(Guid.NewGuid(), HelpKind.Video, Text("Title"), Text("Body"), Admin, At);

        var act = () => content.AttachMedia(Guid.NewGuid(), HelpMediaType.Video, "key", null, null, Admin);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.HelpNoCaptions);
        ex.ExternalCode.Should().Be("E-FAQHC-UNSUPPORTED-FORMAT");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-08")]
    [Trait("AC", "AC-01")]
    public void AttachMedia_WithCaptions_Succeeds()
    {
        var content = HelpContent.Create(Guid.NewGuid(), HelpKind.Video, Text("Title"), Text("Body"), Admin, At);

        var media = content.AttachMedia(Guid.NewGuid(), HelpMediaType.Video, "key", "captions-ref", null, Admin);

        media.Type.Should().Be(HelpMediaType.Video);
        content.Media.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-08")]
    public void AttachMedia_WithTextAlternativeOnly_Succeeds()
    {
        var content = HelpContent.Create(Guid.NewGuid(), HelpKind.InteractiveGuide, Text("Title"), Text("Body"), Admin, At);

        var media = content.AttachMedia(Guid.NewGuid(), HelpMediaType.InteractiveGuide, "key", null, "A text alternative.", Admin);

        media.TextAlternative.Should().Be("A text alternative.");
    }
}
