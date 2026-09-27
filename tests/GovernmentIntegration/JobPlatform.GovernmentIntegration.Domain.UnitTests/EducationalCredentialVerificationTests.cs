using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class EducationalCredentialVerificationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Credential ValidCredential() => new("Birzeit University", "BSc Computer Science", 2020);

    [Fact]
    [Trait("Story", "US-3.4.2-03")]
    [Trait("AC", "AC-01")]
    public void MarkVerified_FromRequested_RaisesImportedEventWithVerifiedOutcome()
    {
        var verification = EducationalCredentialVerification.Request(Guid.NewGuid(), Guid.NewGuid(), ValidCredential(), At);

        verification.MarkVerified(At);

        verification.Status.Should().Be(EducationalStatus.Verified);
        verification.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EducationalCredentialVerificationImportedDomainEvent>()
            .Which.Outcome.Should().Be("Verified");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-03")]
    [Trait("AC", "AC-03")]
    public void MarkUnverified_IsNotAFailure_KeepsTheRecordWithUnverifiedOutcome()
    {
        var verification = EducationalCredentialVerification.Request(Guid.NewGuid(), Guid.NewGuid(), ValidCredential(), At);

        verification.MarkUnverified(At);

        verification.Status.Should().Be(EducationalStatus.Unverified);
        verification.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EducationalCredentialVerificationImportedDomainEvent>()
            .Which.Outcome.Should().Be("Unverified");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-03")]
    [Trait("AC", "AC-02")]
    public void RecordInstitutionUnavailable_SetsInstitutionUnavailable()
    {
        var verification = EducationalCredentialVerification.Request(Guid.NewGuid(), Guid.NewGuid(), ValidCredential(), At);

        verification.RecordAttempt();
        verification.RecordInstitutionUnavailable();

        verification.Status.Should().Be(EducationalStatus.InstitutionUnavailable);
    }

    [Fact]
    public void Request_WithEmptySubject_ThrowsSubjectRequired()
    {
        var act = () => EducationalCredentialVerification.Request(Guid.NewGuid(), Guid.Empty, ValidCredential(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-GI-SUBJECT-REQUIRED");
    }
}
