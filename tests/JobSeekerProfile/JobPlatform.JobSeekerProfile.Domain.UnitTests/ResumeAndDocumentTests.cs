using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain.UnitTests;

public class ResumeTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static FileReference Pdf(long size = 1024) => new("key", "cv.pdf", size, "application/pdf", "hash-1");

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    [Trait("AC", "AC-03")]
    public void Upload_UnsupportedFormat_ThrowsUnsupportedFormat()
    {
        var act = () => Resume.Upload(Guid.NewGuid(), ProfileId, new FileReference("key", "cv.exe", 100, "application/x-msdownload", "h"), ActorId, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ResumeUnsupportedFormat);
        ex.ExternalCode.Should().Be(ErrorCodes.UnsupportedFormat);
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    [Trait("AC", "AC-03")]
    public void Upload_TooLarge_ThrowsTooLarge()
    {
        var act = () => Resume.Upload(Guid.NewGuid(), ProfileId, Pdf(Resume.MaxSizeBytes + 1), ActorId, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ResumeTooLarge);
        ex.ExternalCode.Should().Be(ErrorCodes.TooLarge);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    [Trait("AC", "AC-01")]
    public void Upload_Success_RaisesResumeCreatedEvent_AndIsCurrent()
    {
        var resume = Resume.Upload(Guid.NewGuid(), ProfileId, Pdf(), ActorId, At);

        resume.IsCurrent.Should().BeTrue();
        resume.Format.Should().Be(ResumeFormat.Pdf);
        resume.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ResumeCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-10")]
    [Trait("AC", "AC-04")]
    public void MarkSuperseded_SetsIsCurrentFalse()
    {
        var resume = Resume.Upload(Guid.NewGuid(), ProfileId, Pdf(), ActorId, At);

        resume.MarkSuperseded();

        resume.IsCurrent.Should().BeFalse();
    }
}

public class SupplementaryDocumentTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    [Trait("Story", "US-3.1.1-09")]
    [Trait("AC", "AC-02")]
    public void Attach_UnsupportedFormat_ThrowsUnsupportedFormat()
    {
        var act = () => SupplementaryDocument.Attach(Guid.NewGuid(), DocumentOwnerType.JobSeekerProfile, OwnerId,
            new FileReference("k", "f.exe", 10, "application/x-msdownload", "h"), "certificate", At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.DocumentUnsupportedFormat);
        ex.ExternalCode.Should().Be(ErrorCodes.UnsupportedFormat);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-08")]
    [Trait("AC", "AC-02")]
    public void Attach_TooLarge_ForCompanyOwner_UsesEmployerErrorCodeFamily()
    {
        var act = () => SupplementaryDocument.Attach(Guid.NewGuid(), DocumentOwnerType.Company, OwnerId,
            new FileReference("k", "f.pdf", SupplementaryDocument.MaxSizeBytes + 1, "application/pdf", "h"), "registration", At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.ExternalCode.Should().Be(ErrorCodes.CompanyTooLarge);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-09")]
    [Trait("AC", "AC-01")]
    public void Attach_Success_SetsFields()
    {
        var document = SupplementaryDocument.Attach(Guid.NewGuid(), DocumentOwnerType.JobSeekerProfile, OwnerId,
            new FileReference("k", "cert.pdf", 100, "application/pdf", "h"), "certificate", At);

        document.OwnerType.Should().Be(DocumentOwnerType.JobSeekerProfile);
        document.DocumentType.Should().Be("certificate");
    }

    [Fact]
    public void EnsureOwnedBy_NonOwner_ThrowsForbidden()
    {
        var document = SupplementaryDocument.Attach(Guid.NewGuid(), DocumentOwnerType.JobSeekerProfile, OwnerId,
            new FileReference("k", "cert.pdf", 100, "application/pdf", "h"), "certificate", At);

        var act = () => document.EnsureOwnedBy(new Actor(Guid.NewGuid()));

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.Forbidden);
    }
}
