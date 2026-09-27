using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.EmployerOnboarding.Domain.UnitTests;

public class CompanyMediaAndDocumentTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static FileReference File(long size = 1024, string contentType = "image/png", string sha256 = "abc") =>
        new("key/logo.png", "logo.png", size, contentType, sha256);

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-01")]
    public void Attach_ValidLogo_Succeeds()
    {
        var employerId = Guid.NewGuid();

        var media = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, File(), new Actor(employerId, false), At);

        media.Kind.Should().Be(MediaKind.Logo);
        media.IsPrimaryLogo.Should().BeFalse();
        media.DomainEvents.Should().HaveCount(2).And.Contain(e => e is CompanyMediaAndDocumentCreatedDomainEvent);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-01")]
    public void Attach_OversizedFile_ThrowsTooLarge()
    {
        var employerId = Guid.NewGuid();

        var act = () => CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, File(size: CompanyMediaAndDocument.MaxSizeBytes + 1),
            new Actor(employerId, false), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.MediaTooLarge);
        ex.ExternalCode.Should().Be("E-ERPM-TOO-LARGE");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-02")]
    public void Attach_UnsupportedFormat_ThrowsUnsupportedFormat()
    {
        var employerId = Guid.NewGuid();

        var act = () => CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, File(contentType: "application/zip"),
            new Actor(employerId, false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.MediaUnsupportedFormat);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    public void Attach_RegistrationDocument_AcceptsOnlyPdf()
    {
        var employerId = Guid.NewGuid();

        var pdf = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.RegistrationDocument, File(contentType: "application/pdf"),
            new Actor(employerId, false), At);
        var act = () => CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.RegistrationDocument, File(contentType: "image/png"),
            new Actor(employerId, false), At);

        pdf.Kind.Should().Be(MediaKind.RegistrationDocument);
        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.MediaUnsupportedFormat);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-04")]
    public void Attach_ByNonOwner_ThrowsForbidden()
    {
        var act = () => CompanyMediaAndDocument.Attach(Guid.NewGuid(), Guid.NewGuid(), MediaKind.Logo, File(), new Actor(Guid.NewGuid(), false), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.MediaNotOwner);
        ex.ExternalCode.Should().Be("E-ERPM-FORBIDDEN");
    }

    [Fact]
    public void SetAsPrimaryLogo_OnNonLogoKind_ThrowsUnsupportedFormat()
    {
        var employerId = Guid.NewGuid();
        var doc = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.RegistrationDocument, File(contentType: "application/pdf"),
            new Actor(employerId, false), At);

        var act = () => doc.SetAsPrimaryLogo(new Actor(employerId, false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.MediaUnsupportedFormat);
    }

    [Fact]
    public void SetAsPrimaryLogo_ThenUnset_TogglesFlag()
    {
        var employerId = Guid.NewGuid();
        var logo = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, File(), new Actor(employerId, false), At);

        logo.SetAsPrimaryLogo(new Actor(employerId, false), At);
        logo.IsPrimaryLogo.Should().BeTrue();

        logo.UnsetPrimaryLogo();
        logo.IsPrimaryLogo.Should().BeFalse();
    }

    [Fact]
    public void Remove_ByOwner_MarksRemovedAndClearsPrimaryFlag()
    {
        var employerId = Guid.NewGuid();
        var logo = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, File(), new Actor(employerId, false), At);
        logo.SetAsPrimaryLogo(new Actor(employerId, false), At);

        logo.Remove(new Actor(employerId, false), At);

        logo.IsRemoved.Should().BeTrue();
        logo.IsPrimaryLogo.Should().BeFalse();
    }
}
