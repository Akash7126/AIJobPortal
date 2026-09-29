using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;
using JobPlatform.GovernmentIntegration.Application.Handlers.Verifications;
using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

public class VerificationsHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-01")]
    public async Task RequestGovernmentVerification_AuthorisedComponentAndMatch_RecordsVerified()
    {
        var store = new FakeStore();
        var mol = new FakeMolRegistryClient { NextSubjectOutcome = SourceCallOutcome.Match };
        var handler = new RequestGovernmentVerificationHandler(store, store, new GovernmentDataAccessPolicy(), mol, new FakePefClient(),
            new FakeGovernmentDatabaseClient(), Kit.Clock());
        var command = new RequestGovernmentVerificationCommand(GovernmentDataAccessPolicy.EmployerOnboardingComponent, SubjectType.Employer,
            Guid.NewGuid(), SourceSystem.MoL, AccessPurpose.EmployerVerification);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.GovernmentVerificationData.Single().Status.Should().Be(GovernmentDataStatus.Verified);
        store.AccessLog.Should().ContainSingle(l => l.Decision == "Allow");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    [Trait("AC", "AC-02")]
    public async Task RequestGovernmentVerification_UnauthorisedComponent_ReturnsForbiddenAndLogsDeny()
    {
        var store = new FakeStore();
        var handler = new RequestGovernmentVerificationHandler(store, store, new GovernmentDataAccessPolicy(), new FakeMolRegistryClient(),
            new FakePefClient(), new FakeGovernmentDatabaseClient(), Kit.Clock());
        var command = new RequestGovernmentVerificationCommand("unknown-component", SubjectType.Employer, Guid.NewGuid(), SourceSystem.MoL,
            AccessPurpose.EmployerVerification);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("E-GDI-FORBIDDEN");
        store.GovernmentVerificationData.Should().BeEmpty();
        store.AccessLog.Should().ContainSingle(l => l.Decision == "Deny");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    [Trait("AC", "AC-03")]
    public async Task RequestGovernmentVerification_NoMatch_IsNotAFailure()
    {
        var store = new FakeStore();
        var mol = new FakeMolRegistryClient { NextSubjectOutcome = SourceCallOutcome.NoMatch };
        var handler = new RequestGovernmentVerificationHandler(store, store, new GovernmentDataAccessPolicy(), mol, new FakePefClient(),
            new FakeGovernmentDatabaseClient(), Kit.Clock());
        var command = new RequestGovernmentVerificationCommand(GovernmentDataAccessPolicy.EmployerOnboardingComponent, SubjectType.Employer,
            Guid.NewGuid(), SourceSystem.MoL, AccessPurpose.EmployerVerification);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.GovernmentVerificationData.Single().Status.Should().Be(GovernmentDataStatus.NoMatch);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-03")]
    [Trait("AC", "AC-01")]
    public async Task RequestEducationalCredentialVerification_Match_MarksVerified()
    {
        var store = new FakeStore();
        var client = new FakeEducationalInstitutionClient { NextOutcome = SourceCallOutcome.Match };
        var handler = new RequestEducationalCredentialVerificationHandler(store, store, new GovernmentDataAccessPolicy(), client, Kit.Clock());
        var command = new RequestEducationalCredentialVerificationCommand(GovernmentDataAccessPolicy.JobSeekerProfileComponent, Guid.NewGuid(),
            "Birzeit University", "BSc CS", 2020);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.EducationalCredentialVerifications.Single().Status.Should().Be(EducationalStatus.Verified);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-04")]
    [Trait("AC", "AC-01")]
    public async Task RequestIdentityVerification_AmbiguousMatch_MarksUnverifiedWithAmbiguousReason()
    {
        var store = new FakeStore();
        var client = new FakeGovernmentIdClient { NextOutcome = SourceCallOutcome.NoMatch, NextAmbiguous = true };
        var handler = new RequestIdentityVerificationHandler(store, store, new GovernmentDataAccessPolicy(), client, Kit.Clock());
        var command = new RequestIdentityVerificationCommand(GovernmentDataAccessPolicy.JobSeekerProfileComponent, SubjectType.JobSeekerProfile,
            Guid.NewGuid(), "900-1", "Sara", new DateOnly(1995, 1, 1));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var record = store.IdentityVerifications.Single();
        record.Status.Should().Be(IdentityStatus.Unverified);
        record.UnverifiedReason.Should().Be(UnverifiedReasons.Ambiguous);
    }

    [Fact]
    [Trait("Story", "US-2.5-03")]
    [Trait("AC", "AC-03")]
    public async Task PurgeExpiredGovernmentData_PurgesOnlyExpiredVerifiedRecords()
    {
        var store = new FakeStore();
        var now = Kit.Clock().GetUtcNow().UtcDateTime;
        var expired = GovernmentVerificationData.Request(Guid.NewGuid(), new Subject(SubjectType.Employer, Guid.NewGuid()), SourceSystem.MoL,
            AccessPurpose.EmployerVerification, now.AddMonths(-13));
        expired.RecordMatch(new[] { new VerifiedField("k", "v") }, now.AddMonths(-13));
        store.Add(expired);
        var handler = new PurgeExpiredGovernmentDataHandler(store, Kit.Clock());

        var result = await handler.Handle(new PurgeExpiredGovernmentDataCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
        expired.VerifiedFields.Should().BeEmpty();
    }
}
