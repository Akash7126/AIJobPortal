using JobPlatform.AuditLogging.Application.Ingestion;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.Audit;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;

namespace JobPlatform.AuditLogging.Application.UnitTests;

public class IngestionHandlerTests
{
    private readonly FakeStore _store = new();
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Employer = Guid.NewGuid();
    private static readonly Guid Platform = Guid.NewGuid();
    private static readonly DateTime T = Ids.T0;

    private static (Guid Id, DateTime At, Guid Corr, Guid? Cause) H() => (Guid.NewGuid(), T, Guid.NewGuid(), null);

    // ------------------------------------------------------------------ BC-03 / access and admin logs

    [Fact]
    [Trait("Story", "US-3.1.5-05")]
    [Trait("AC", "AC-01")]
    public async Task AccountCreated_RecordsAdminOnlyAccessEntry()
    {
        var (id, at, corr, cause) = H();
        var account = Guid.NewGuid();

        await new AccountCreatedAuditHandler(_store.Ingestion()).Handle(new AccountCreatedIntegrationEvent(id, at, corr, cause, account, account, ActorType.JobSeeker, 1), default);

        var entry = _store.Entries.Should().ContainSingle().Which;
        entry.Category.Should().Be(AuditCategory.Access);
        entry.OwnerType.Should().Be(OwnerType.AdminOnly);
        entry.SubjectId.Should().Be(account.ToString());
        entry.Action.Should().Be("AccountCreated");
        entry.SourceBc.Should().Be("account-identity");
    }

    [Fact]
    public async Task AccountApproved_And_UserAccountApproved_AreRecorded()
    {
        var (id, at, corr, cause) = H();
        var account = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var ingestion = _store.Ingestion();

        await new AccountApprovedAuditHandler(ingestion).Handle(new AccountApprovedIntegrationEvent(id, at, corr, cause, account, admin, ActorType.Employer, 2), default);
        await new UserAccountApprovedAuditHandler(ingestion).Handle(new UserAccountApprovedIntegrationEvent(Guid.NewGuid(), at, corr, cause, account, admin, "Pending", "Active", 3), default);
        await new ApiCredentialCreatedAuditHandler(ingestion).Handle(new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), at, corr, cause, Guid.NewGuid(), account, admin, at.AddYears(1), 1), default);

        _store.Entries.Select(e => e.Category).Should().BeEquivalentTo(new[] { AuditCategory.Access, AuditCategory.AdminAction, AuditCategory.AdminAction });
        _store.Entries.Single(e => e.Action == "UserAccountStandingChanged").Details.Should().Contain("from", "Pending").And.Contain("to", "Active");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-05")]
    [Trait("AC", "AC-01")]
    public async Task PlatformAdministrationEvents_AreRecordedAsAdminActions()
    {
        var ingestion = _store.Ingestion();
        var admin = Guid.NewGuid();
        var (id, at, corr, cause) = H();

        await new PlatformEntityRecordCreatedAuditHandler(ingestion).Handle(new PlatformEntityRecordCreatedIntegrationEvent(id, at, corr, cause, Guid.NewGuid(), admin, "Region", 1), default);
        await new PlatformTaxonomyUpdatedAuditHandler(ingestion).Handle(new PlatformTaxonomyUpdatedIntegrationEvent(Guid.NewGuid(), at, corr, cause, Guid.NewGuid(), "Draft", "Active", admin, "Skills", 1, 2, new[] { "S1" }, 2), default);
        await new JobOfferingSuspendedAuditHandler(ingestion).Handle(new JobOfferingSuspendedIntegrationEvent(Guid.NewGuid(), at, corr, cause, Guid.NewGuid(), Guid.NewGuid(), admin, "policy", 1), default);

        _store.Entries.Should().HaveCount(3).And.OnlyContain(e => e.Category == AuditCategory.AdminAction && e.OwnerType == OwnerType.AdminOnly);
        _store.Entries.Select(e => e.Action).Should().BeEquivalentTo("PlatformEntityRecordCreated", "PlatformTaxonomyUpdated", "JobOfferingSuspended");
    }

    [Fact]
    public async Task ProfileCreated_IsRecordedAsProfileEntry()
    {
        var (id, at, corr, cause) = H();

        await new ProfileCreatedAuditHandler(_store.Ingestion()).Handle(new ProfileCreatedIntegrationEvent(id, at, corr, cause, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Gaza", 1), default);

        _store.Entries.Should().ContainSingle().Which.Category.Should().Be(AuditCategory.Profile);
    }

    // ------------------------------------------------------------------ BC-05 / BC-01

    [Fact]
    [Trait("Story", "US-3.1.2-07")]
    [Trait("AC", "AC-01")]
    public async Task EmployerRegistrationApproved_RecordsAuditAndOpensDashboard()
    {
        var (id, at, corr, cause) = H();

        await new EmployerRegistrationApprovedAuditHandler(_store.Ingestion()).Handle(
            new EmployerRegistrationApprovedIntegrationEvent(id, at, corr, cause, Guid.NewGuid(), Guid.NewGuid(), Employer, 1), default);

        _store.Entries.Should().ContainSingle().Which.Category.Should().Be(AuditCategory.AdminAction);
        _store.Dashboards.Should().ContainSingle().Which.RegistrationApproved.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.4.2-05")]
    [Trait("AC", "AC-01")]
    public async Task GovernmentEvents_AreRecordedInTheGovernmentTrail()
    {
        var ingestion = _store.Ingestion();
        var (id, at, corr, cause) = H();

        await new EmployerVerificationApprovedAuditHandler(ingestion).Handle(new EmployerVerificationApprovedIntegrationEvent(id, at, corr, cause, Guid.NewGuid(), Employer, Guid.NewGuid(), "Automatic", 1), default);
        await new GovernmentVerificationDataImportedAuditHandler(ingestion).Handle(new GovernmentVerificationDataImportedIntegrationEvent(Guid.NewGuid(), at, corr, cause, Guid.NewGuid(), "Employer", Employer, "NoMatch", 1), default);

        _store.Entries.Should().HaveCount(2).And.OnlyContain(e => e.Category == AuditCategory.GovernmentExchange);
        _store.Entries.Single(e => e.Action == "GovernmentQueryCompleted").Outcome.Should().Be(AuditOutcome.Failure);
        _store.Entries.Single(e => e.Action == "EmployerVerificationApproved").Outcome.Should().Be(AuditOutcome.Success);
    }

    // ------------------------------------------------------------------ BC-02 sync status, usage, submissions

    private static JobDataImportedIntegrationEvent Imported(string platformJobId, bool isUpdate = false, long version = 1, Guid? messageId = null) =>
        new(messageId ?? Guid.NewGuid(), T, Guid.NewGuid(), null, Guid.NewGuid(), Platform, Partner, platformJobId, "src-1", "Engineer", "Summary",
            new[] { "C#" }, "FullTime", "OnSite", null, "Gaza", null, "Visible", isUpdate, version);

    [Fact]
    [Trait("Story", "US-3.1.3-07")]
    [Trait("AC", "AC-01")]
    public async Task JobDataImported_RecordsPartnerSubmissionAndAdminJobAudit_AndSyncsTheJob()
    {
        await new JobDataImportedAuditHandler(_store.Ingestion()).Handle(Imported("PJ-1"), default);

        _store.Entries.Should().HaveCount(2);
        var submission = _store.Entries.Single(e => e.Category == AuditCategory.Submission);
        submission.OwnerType.Should().Be(OwnerType.Partner);
        submission.OwnerId.Should().Be(Partner);
        _store.Entries.Single(e => e.Category == AuditCategory.JobAudit).OwnerType.Should().Be(OwnerType.AdminOnly);
        _store.Syncs.Should().ContainSingle().Which.Status.Should().Be(SyncStatus.Synced);
        _store.Usage.Should().ContainSingle().Which.Submitted.Should().Be(1);
    }

    [Fact]
    public async Task JobDataImported_Redelivery_DoesNotDuplicateEntries()
    {
        var ingestion = _store.Ingestion();
        var handler = new JobDataImportedAuditHandler(ingestion);
        var message = Guid.NewGuid();

        await handler.Handle(Imported("PJ-1", messageId: message), default);
        await handler.Handle(Imported("PJ-1", messageId: message), default);

        _store.Entries.Should().HaveCount(2);
        _store.Syncs.Should().ContainSingle();
    }

    [Fact]
    public async Task JobDataImported_UpdateOfKnownJob_DoesNotCountAnotherSubmission()
    {
        var handler = new JobDataImportedAuditHandler(_store.Ingestion());

        await handler.Handle(Imported("PJ-1"), default);
        await handler.Handle(Imported("PJ-1", isUpdate: true, version: 2), default);

        _store.Usage.Single().Submitted.Should().Be(1);
        _store.Entries.Count(e => e.Action == "JobUpdateImported").Should().Be(2);
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Deleted")]
    [InlineData("Deactivated")]
    public async Task JobPostAttributionUpdated_Terminal_ArchivesTheSyncedJob(string toStatus)
    {
        var ingestion = _store.Ingestion();
        await new JobDataImportedAuditHandler(ingestion).Handle(Imported("PJ-1"), default);

        await new JobPostAttributionUpdatedAuditHandler(ingestion).Handle(
            new JobPostAttributionUpdatedIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, Guid.NewGuid(), "Active", toStatus, Partner, "PJ-1", null, null, 2), default);

        _store.Syncs.Single().Status.Should().Be(SyncStatus.Archived);
        _store.Entries.Should().Contain(e => e.Action == "JobPostAttributionUpdated" && e.Category == AuditCategory.JobAudit);
    }

    [Fact]
    public async Task JobPostAttributionUpdated_ForUnknownJob_IsRecordedButIgnoredByTheProjection()
    {
        await new JobPostAttributionUpdatedAuditHandler(_store.Ingestion()).Handle(
            new JobPostAttributionUpdatedIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, Guid.NewGuid(), "Active", "Closed", Partner, "PJ-404", null, null, 2), default);

        _store.Syncs.Should().BeEmpty();
        _store.Entries.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.4.1-04")]
    [Trait("AC", "AC-01")]
    public async Task AuditRecord_SyncFailure_MarksTheJobFailed_ThenRetryAndSuccessSyncIt()
    {
        var handler = new AuditRecordHandler(_store.Ingestion());
        AuditRecordIntegrationEvent Record(string outcome, string? code, string? retry) => new(Guid.NewGuid(), T, Guid.NewGuid(), null, "external-integration", "SyncError", Partner,
            "Job", "PJ-9", $"Partner:{Partner}", "Sync", outcome, code,
            new Dictionary<string, string> { ["platformJobId"] = "PJ-9", ["sourcePlatformId"] = Platform.ToString(), ["retry"] = retry ?? "false" });

        await handler.Handle(Record("Failure", "E-SYNC-TIMEOUT", null), default);
        _store.Syncs.Single().Status.Should().Be(SyncStatus.Failed);
        _store.Syncs.Single().ReasonCode.Should().Be("E-SYNC-TIMEOUT");

        await handler.Handle(Record("Failure", "E-SYNC-TIMEOUT-2", "true"), default);
        _store.Syncs.Single().Status.Should().Be(SyncStatus.Failed, "the retry went Failed to Pending and failed again");
        _store.Syncs.Single().ReasonCode.Should().Be("E-SYNC-TIMEOUT-2");

        await handler.Handle(Record("Success", null, "true"), default);
        _store.Syncs.Single().Status.Should().Be(SyncStatus.Synced);
        _store.Entries.Should().HaveCount(3).And.OnlyContain(e => e.Category == AuditCategory.SyncError && e.OwnerId == Partner);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-06")]
    [Trait("AC", "AC-01")]
    public async Task AuditRecord_ApiCall_IsStoredWithOutcomeCodeAndOwner()
    {
        var evt = new AuditRecordIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, "external-integration", "ApiCall", Partner, "ApiRequest", "req-1",
            $"Partner:{Partner}", "PostJobs", "Failure", "E-TPJPRI-INVALID-FIELD", new Dictionary<string, string> { ["endpoint"] = "/jobs" });

        await new AuditRecordHandler(_store.Ingestion()).Handle(evt, default);

        var entry = _store.Entries.Should().ContainSingle().Which;
        entry.Category.Should().Be(AuditCategory.ApiCall);
        entry.Outcome.Should().Be(AuditOutcome.Failure);
        entry.Code.Should().Be("E-TPJPRI-INVALID-FIELD");
        entry.OwnerId.Should().Be(Partner);
    }

    [Fact]
    public async Task AuditRecord_WithPersonalDataInDetails_IsRedactedNotRejected()
    {
        var evt = new AuditRecordIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, "account-identity", "Access", null, "Login", "x", null, "LoginFailed", "Denied",
            "E-AAFR-INVALID-CREDENTIALS", new Dictionary<string, string> { ["email"] = "someone@example.com", ["reason"] = "wrong password" });

        await new AuditRecordHandler(_store.Ingestion()).Handle(evt, default);

        var entry = _store.Entries.Single();
        entry.Details.Should().NotContainKey("email").And.Contain("redacted", "1").And.Contain("reason", "wrong password");
        entry.Outcome.Should().Be(AuditOutcome.Denied);
        entry.OwnerType.Should().Be(OwnerType.AdminOnly);
    }

    [Fact]
    public async Task AuditRecord_UnknownCategory_Throws_SoTheInboxCanRetryAndFinallyFlagIt()
    {
        var evt = new AuditRecordIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, "x", "Bogus", null, "S", "1", null, "A", "Success", null, null);

        var act = () => new AuditRecordHandler(_store.Ingestion()).Handle(evt, default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ------------------------------------------------------------------ BC-09 job status history and dashboard

    [Fact]
    [Trait("Story", "US-3.2.4-02")]
    [Trait("AC", "AC-01")]
    public async Task JobPostingLifecycle_BuildsStatusHistoryAndDashboard()
    {
        var ingestion = _store.Ingestion();
        var job = Guid.NewGuid();

        await new JobPostingCreatedAuditHandler(ingestion).Handle(new JobPostingCreatedIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, job, Employer, Employer, "Draft",
            "Engineer", "IT", new[] { "C#" }, "Public", "Employer", "Gaza", null, null, 1), default);
        await new JobPostingStatusUpdatedAuditHandler(ingestion).Handle(new JobPostingStatusUpdatedIntegrationEvent(Guid.NewGuid(), T.AddHours(1), Guid.NewGuid(), null, job, job,
            Employer, "Draft", "Active", Employer, null, 2), default);
        await new JobPostingUpdatedAuditHandler(ingestion).Handle(new JobPostingUpdatedIntegrationEvent(Guid.NewGuid(), T.AddHours(2), Guid.NewGuid(), null, job, Employer,
            "Active", "Active", Employer, new[] { "Title" }, 3), default);

        _store.History.Select(h => (h.FromStatus, h.ToStatus)).Should().Equal((null, "Draft"), ("Draft", "Active"));
        _store.Dashboards.Single().Postings.Single().Status.Should().Be("Active");
        _store.Entries.Should().HaveCount(2).And.OnlyContain(e => e.Category == AuditCategory.JobStatus && e.OwnerId == Employer);
    }

    [Fact]
    public async Task JobPostingUpdated_WithStatusChange_AddsHistoryRow()
    {
        var ingestion = _store.Ingestion();
        var job = Guid.NewGuid();

        await new JobPostingUpdatedAuditHandler(ingestion).Handle(new JobPostingUpdatedIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, job, Employer, "Active", "Closed",
            Employer, new[] { "Status" }, 4), default);

        _store.History.Should().ContainSingle().Which.ToStatus.Should().Be("Closed");
    }

    // ------------------------------------------------------------------ BC-11 candidate insight and shortlists

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-01")]
    public async Task CandidateInsightComputed_StoresInsightAndEntry_AndRefreshesOnRecompute()
    {
        var ingestion = _store.Ingestion();
        var handler = new CandidateInsightComputedAuditHandler(ingestion);
        var insightId = Guid.NewGuid();
        var job = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        CandidateInsightComputedIntegrationEvent Evt(DateTime at, string availability) => new(Guid.NewGuid(), at, Guid.NewGuid(), null, insightId, job, Employer, Employer, candidate,
            availability, 1200m, 80m, new[] { "expectedSalary" }, 1);

        await handler.Handle(Evt(T, "Immediate"), default);
        await handler.Handle(Evt(T.AddHours(1), "In a month"), default);

        _store.Insights.Should().ContainSingle().Which.Availability.Should().Be("In a month");
        _store.Insights.Single().WithheldFields.Should().ContainSingle("expectedSalary");
        _store.Entries.Should().HaveCount(2).And.OnlyContain(e => e.Category == AuditCategory.Insight);
    }

    [Fact]
    public async Task TalentPoolEntryCreated_CountsAShortlistOncePerMessage()
    {
        var ingestion = _store.Ingestion();
        var handler = new TalentPoolEntryCreatedAuditHandler(ingestion);
        var message = Guid.NewGuid();
        TalentPoolEntryCreatedIntegrationEvent Evt() => new(message, T, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Employer, Employer, 1);

        await handler.Handle(Evt(), default);
        await handler.Handle(Evt(), default);

        _store.Dashboards.Single().ShortlistCount.Should().Be(1);
        _store.Entries.Should().ContainSingle();
    }

    // ------------------------------------------------------------------ BC-13 notifications

    [Theory]
    [Trait("Story", "US-3.6.1-04")]
    [Trait("AC", "AC-01")]
    [InlineData("Email", AuditCategory.Email)]
    [InlineData("Sms", AuditCategory.Sms)]
    [InlineData("InApp", AuditCategory.Notification)]
    public async Task NotificationSent_LogsMaskedRecipientAndAudits(string channel, AuditCategory category)
    {
        var recipient = Guid.NewGuid();
        var id = Guid.NewGuid();

        await new NotificationSentAuditHandler(_store.Ingestion()).Handle(new NotificationSentIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, id, channel, "Welcome",
            recipient, "s***@example.com", "Hello", "Sent", 1), default);

        _store.Notifications.Should().ContainSingle().Which.MaskedRecipient.Should().Be("s***@example.com");
        var entry = _store.Entries.Should().ContainSingle().Which;
        entry.Category.Should().Be(category);
        entry.OwnerType.Should().Be(category == AuditCategory.Notification ? OwnerType.User : OwnerType.AdminOnly);
        entry.Details.Should().NotContainKey("recipient");
    }

    [Fact]
    public async Task NotificationStatusUpdated_UpdatesTheLogAndAudits()
    {
        var ingestion = _store.Ingestion();
        var id = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        await new NotificationSentAuditHandler(ingestion).Handle(new NotificationSentIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null, id, "InApp", "News", recipient, "user", null, "Unread", 1), default);

        await new NotificationStatusUpdatedAuditHandler(ingestion).Handle(new NotificationStatusUpdatedIntegrationEvent(Guid.NewGuid(), T.AddMinutes(5), Guid.NewGuid(), null, id, "Unread", "Read", recipient, 2), default);

        _store.Notifications.Single().Status.Should().Be("Read");
        _store.Entries.Count(e => e.Action == "NotificationStatusChanged").Should().Be(1);
    }

    [Fact]
    public async Task NotificationStatusUpdated_BeforeTheNotificationIsKnown_ThrowsSoTheInboxRetriesLater()
    {
        var act = () => new NotificationStatusUpdatedAuditHandler(_store.Ingestion()).Handle(new NotificationStatusUpdatedIntegrationEvent(Guid.NewGuid(), T, Guid.NewGuid(), null,
            Guid.NewGuid(), "Unread", "Read", Guid.NewGuid(), 2), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
