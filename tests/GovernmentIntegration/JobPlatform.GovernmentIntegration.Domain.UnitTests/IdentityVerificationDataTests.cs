using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class IdentityVerificationDataTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Subject ValidSubject() => new(SubjectType.JobSeekerProfile, Guid.NewGuid());

    private static IdentityClaim ValidClaim() => new("900-1234567-1", "Sara Ahmad", new DateOnly(1995, 5, 1));

    [Fact]
    [Trait("Story", "US-3.4.2-04")]
    [Trait("AC", "AC-01")]
    public void MarkVerified_FromRequested_RaisesImportedEvent()
    {
        var verification = IdentityVerificationData.Request(Guid.NewGuid(), ValidSubject(), ValidClaim(), At);

        verification.MarkVerified(At);

        verification.Status.Should().Be(IdentityStatus.Verified);
        verification.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IdentityVerificationDataImportedDomainEvent>()
            .Which.Outcome.Should().Be("Verified");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-04")]
    public void MarkUnverified_WithAmbiguousReason_SetsUnverifiedReason()
    {
        var verification = IdentityVerificationData.Request(Guid.NewGuid(), ValidSubject(), ValidClaim(), At);

        verification.MarkUnverified(UnverifiedReasons.Ambiguous, At);

        verification.Status.Should().Be(IdentityStatus.Unverified);
        verification.UnverifiedReason.Should().Be(UnverifiedReasons.Ambiguous);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-04")]
    [Trait("AC", "AC-02")]
    public void RecordSystemUnavailable_SetsSystemUnavailable()
    {
        var verification = IdentityVerificationData.Request(Guid.NewGuid(), ValidSubject(), ValidClaim(), At);

        verification.RecordAttempt();
        verification.RecordSystemUnavailable();

        verification.Status.Should().Be(IdentityStatus.SystemUnavailable);
    }
}
