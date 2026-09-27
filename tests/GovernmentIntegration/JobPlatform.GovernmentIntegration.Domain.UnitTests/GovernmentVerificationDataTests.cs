using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class GovernmentVerificationDataTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Subject ValidSubject() => new(SubjectType.JobSeekerProfile, Guid.NewGuid());

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-01")]
    public void Request_WithSubject_StartsRequestedAndSetsRetention()
    {
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), ValidSubject(), SourceSystem.MoL, AccessPurpose.EmployerVerification, At);

        data.Status.Should().Be(GovernmentDataStatus.Requested);
        data.RetentionExpiresAtUtc.Should().Be(At.AddMonths(12));
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    public void Request_WithEmptySubjectId_ThrowsSubjectRequired()
    {
        var act = () => GovernmentVerificationData.Request(Guid.NewGuid(), new Subject(SubjectType.Employer, Guid.Empty), SourceSystem.MoL,
            AccessPurpose.EmployerVerification, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.GovDataSubjectRequired);
        ex.ExternalCode.Should().Be("E-GI-SUBJECT-REQUIRED");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-01")]
    public void RecordMatch_FromRequested_SetsVerifiedAndRaisesImportedEvent()
    {
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), ValidSubject(), SourceSystem.MoL, AccessPurpose.EmployerVerification, At);

        data.RecordMatch(new[] { new VerifiedField("name", "Acme") }, At);

        data.Status.Should().Be(GovernmentDataStatus.Verified);
        data.VerifiedFields.Should().ContainSingle().Which.Value.Should().Be("Acme");
        data.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<GovernmentVerificationDataImportedDomainEvent>()
            .Which.Outcome.Should().Be("Verified");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-03")]
    public void RecordNoMatch_IsNotAFailure_StillRaisesImportedEvent()
    {
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), ValidSubject(), SourceSystem.GovernmentDatabase, AccessPurpose.Enrichment, At);

        data.RecordNoMatch(At);

        data.Status.Should().Be(GovernmentDataStatus.NoMatch);
        data.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<GovernmentVerificationDataImportedDomainEvent>()
            .Which.Outcome.Should().Be("NoMatch");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-02")]
    public void RecordSourceUnavailable_AfterThreeAttempts_ExposesExhausted()
    {
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), ValidSubject(), SourceSystem.PEF, AccessPurpose.CredentialVerification, At);

        data.RecordAttempt();
        data.RecordAttempt();
        data.RecordAttempt();
        data.RecordSourceUnavailable();

        data.AttemptsExhausted.Should().BeTrue();
        data.Status.Should().Be(GovernmentDataStatus.SourceUnavailable);
    }

    [Fact]
    public void RecordMatch_WhenAlreadyResolved_ThrowsNotRequested()
    {
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), ValidSubject(), SourceSystem.MoL, AccessPurpose.EmployerVerification, At);
        data.RecordNoMatch(At);

        var act = () => data.RecordMatch(Array.Empty<VerifiedField>(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.GovDataNotRequested);
    }

    [Fact]
    [Trait("Story", "US-2.5-03")]
    [Trait("AC", "AC-03")]
    public void Purge_ClearsVerifiedFields()
    {
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), ValidSubject(), SourceSystem.MoL, AccessPurpose.EmployerVerification, At);
        data.RecordMatch(new[] { new VerifiedField("name", "Acme") }, At);

        data.Purge();

        data.VerifiedFields.Should().BeEmpty();
    }
}
