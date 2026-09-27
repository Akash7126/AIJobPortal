using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.IntegrationTests;

public class PersistenceTests
{
    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task EmployerVerification_RoundTripsSubmissionAndAttempts()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var verification = EmployerVerification.Request(Guid.NewGuid(), employerId, new Submission("REG-1", "VAT-1", "+970591234567",
                new Dictionary<string, string> { ["extra"] = "value" }));
            verification.RecordAttempt(SourceSystem.MoL, AttemptOutcome.NoMatch, null, Db.T0);
            verification.EscalateToManualReview("no match");
            write.EmployerVerifications.Add(verification);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new EmployerVerificationRepository(read).GetActiveByEmployerAsync(employerId);
        loaded!.Submission.RegistrationNumber.Should().Be("REG-1");
        loaded.Submission.AdditionalFields.Should().ContainKey("extra").WhoseValue.Should().Be("value");
        loaded.Attempts.Should().ContainSingle().Which.Outcome.Should().Be(AttemptOutcome.NoMatch);
        loaded.State.Should().Be(VerificationState.PendingManualReview);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    public async Task GovernmentVerificationData_RoundTripsSubjectAndVerifiedFields()
    {
        await using var database = Db.New();
        var subjectId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var data = GovernmentVerificationData.Request(Guid.NewGuid(), new Subject(SubjectType.JobSeekerProfile, subjectId), SourceSystem.MoL,
                AccessPurpose.EmployerVerification, Db.T0);
            data.RecordMatch(new[] { new VerifiedField("fullName", "Sara Ahmad") }, Db.T0);
            write.GovernmentVerificationData.Add(data);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new GovernmentVerificationDataRepository(read).GetLatestForSubjectAsync(SubjectType.JobSeekerProfile, subjectId);
        loaded!.Subject.SubjectId.Should().Be(subjectId);
        loaded.VerifiedFields.Should().ContainSingle().Which.Value.Should().Be("Sara Ahmad");
        loaded.Status.Should().Be(GovernmentDataStatus.Verified);
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-04")]
    public async Task LegacyData_DuplicateSourceKey_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.LegacyData.Add(LegacyData.Import(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SourceSystem.MoL, "SRC-1", "JobSeeker", "{}"));
        await db.SaveChangesAsync();
        db.LegacyData.Add(LegacyData.Import(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SourceSystem.MoL, "SRC-1", "JobSeeker", "{}"));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    public async Task LegacyData_RoundTripsValidationErrors()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var legacy = LegacyData.Import(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SourceSystem.PEF, "SRC-2", "Employer", "{}");
            legacy.MapToNewSchema("{}");
            legacy.Transform("{}");
            legacy.Validate(new[] { "E-DMIG-INVALID-FIELD" });
            write.LegacyData.Add(legacy);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new LegacyDataRepository(read).GetBySourceKeyAsync(SourceSystem.PEF, "SRC-2");
        loaded!.Stage.Should().Be(LegacyStage.Invalid);
        loaded.ValidationErrors.Should().ContainSingle().Which.Should().Be("E-DMIG-INVALID-FIELD");
    }

    [Fact]
    [Trait("Story", "US-6.1-01")]
    [Trait("AC", "AC-04")]
    public async Task MigrationRun_RoundTripsPhasesAndLog()
    {
        await using var database = Db.New();
        Guid runId;
        await using (var write = database.NewContext())
        {
            var run = MigrationRun.Start(Guid.NewGuid(), Db.Admin, new[] { "Import", "Cleanse" }, Db.T0);
            run.AcceptPhase("ok", Db.T0.AddMinutes(5));
            runId = run.Id;
            write.MigrationRuns.Add(run);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new MigrationRunRepository(read).GetByIdAsync(runId);
        loaded!.Phases.Should().HaveCount(2);
        loaded.Phases.Single(p => p.Name == "Import").Status.Should().Be(MigrationPhaseStatus.Accepted);
        loaded.Phases.Single(p => p.Name == "Cleanse").Status.Should().Be(MigrationPhaseStatus.Running);
        loaded.Log.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    public async Task GovernmentSourceConnection_DuplicateSource_ViolatesUniqueIndex()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        db.GovernmentSourceConnections.Add(GovernmentSourceConnection.Configure(Guid.NewGuid(), Db.Admin, SourceSystem.MoL, "https://mol.example",
            "ApiKey", "ref", true));
        await db.SaveChangesAsync();
        db.GovernmentSourceConnections.Add(GovernmentSourceConnection.Configure(Guid.NewGuid(), Db.Admin, SourceSystem.MoL, "https://mol2.example",
            "ApiKey", "ref2", true));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-2.5-01")]
    public async Task GovernmentSourceConnection_RoundTripsLastKnownGood()
    {
        await using var database = Db.New();
        Guid id;
        await using (var write = database.NewContext())
        {
            var connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), Db.Admin, SourceSystem.PEF, "https://pef.example", "ApiKey", "ref", true);
            connection.RecordSyncSuccess("snapshot-1", Db.T0);
            id = connection.Id;
            write.GovernmentSourceConnections.Add(connection);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new GovernmentSourceConnectionRepository(read).GetByIdAsync(id);
        loaded!.LastKnownGood!.SnapshotRef.Should().Be("snapshot-1");
        loaded.Health.Should().Be(ConnectionHealth.Healthy);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-04")]
    public async Task IdentityVerificationData_RoundTripsClaimAndSubject()
    {
        await using var database = Db.New();
        var subjectId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var claim = new IdentityClaim("900-1", "Sara Ahmad", new DateOnly(1995, 5, 1));
            var identity = IdentityVerificationData.Request(Guid.NewGuid(), new Subject(SubjectType.JobSeekerProfile, subjectId), claim, Db.T0);
            identity.MarkVerified(Db.T0);
            write.IdentityVerifications.Add(identity);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new IdentityVerificationRepository(read).GetLatestForSubjectAsync(subjectId);
        loaded!.IdentityClaim.FullName.Should().Be("Sara Ahmad");
        loaded.Status.Should().Be(IdentityStatus.Verified);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task KnownAccount_RoundTrips()
    {
        await using var database = Db.New();
        var accountId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.KnownAccounts.Add(new KnownAccount(accountId, ActorType.Employer, Db.T0, 1));
            await write.SaveChangesAsync();
        }

        var loaded = await new KnownAccountRepository(database.NewContext()).GetAsync(accountId);
        loaded!.ActorType.Should().Be(ActorType.Employer);
    }

    [Fact]
    [Trait("Story", "US-3.4.2-06")]
    public async Task GovernmentDataAccessLog_Persists()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            new GovernmentDataAccessLogRepository(write).Add(new GovernmentDataAccessLogEntry(Guid.NewGuid(), Db.T0, "employer-onboarding",
                AccessPurpose.EmployerVerification, "Employer:" + Guid.NewGuid(), "Allow", null));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await read.GovernmentDataAccessLog.CountAsync()).Should().Be(1);
    }
}
