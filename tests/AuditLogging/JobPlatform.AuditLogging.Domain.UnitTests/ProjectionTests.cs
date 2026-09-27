using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain.UnitTests;

public class SyncJobStatusTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Partner = Guid.NewGuid();

    private static SyncJobStatus NewJob() => SyncJobStatus.Received("PJ-1", Guid.NewGuid(), Partner, T0);

    [Fact]
    public void Received_StartsPending() => NewJob().Status.Should().Be(SyncStatus.Pending);

    [Fact]
    [Trait("Story", "US-3.1.3-10")]
    [Trait("AC", "AC-01")]
    public void MarkSynced_FromPending_Succeeds()
    {
        var job = NewJob();

        job.MarkSynced(T0.AddMinutes(1), 1).Should().BeTrue();

        job.Status.Should().Be(SyncStatus.Synced);
        job.LastEventVersion.Should().Be(1);
    }

    [Fact]
    public void MarkSynced_WhenAlreadySynced_IsIdempotent()
    {
        var job = NewJob();
        job.MarkSynced(T0, 1);

        job.MarkSynced(T0.AddMinutes(1), 1).Should().BeTrue();

        job.Status.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-04")]
    [Trait("AC", "AC-02")]
    public void FailedJob_RetriesThroughPending_ThenSyncs()
    {
        var job = NewJob();
        job.MarkFailed("E-SYNC-TIMEOUT", T0.AddMinutes(1)).Should().BeTrue();
        job.Status.Should().Be(SyncStatus.Failed);
        job.ReasonCode.Should().Be("E-SYNC-TIMEOUT");

        job.MarkSynced(T0.AddMinutes(2), 2).Should().BeFalse("a failed job must go back through Pending (AL.Sync.RETRY_VIA_PENDING)");
        job.Retry(T0.AddMinutes(3)).Should().BeTrue();
        job.Status.Should().Be(SyncStatus.Pending);
        job.ReasonCode.Should().BeNull();
        job.MarkSynced(T0.AddMinutes(4), 3).Should().BeTrue();
        job.Status.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public void Retry_WhenNotFailed_IsIgnored() => NewJob().Retry(T0).Should().BeFalse();

    [Fact]
    public void FailedJob_CanFailAgainWithANewReason()
    {
        var job = NewJob();
        job.MarkFailed("A", T0);

        job.MarkFailed("B", T0.AddMinutes(1)).Should().BeTrue();

        job.ReasonCode.Should().Be("B");
    }

    [Fact]
    public void Archive_OnlyFromSynced()
    {
        var pending = NewJob();
        pending.Archive(T0, 5).Should().BeFalse();

        var synced = NewJob();
        synced.MarkSynced(T0, 1);
        synced.Archive(T0.AddDays(1), 2).Should().BeTrue();
        synced.Status.Should().Be(SyncStatus.Archived);
    }

    [Fact]
    public void StaleEvent_IsIgnored()
    {
        var job = NewJob();
        job.MarkSynced(T0, 5);

        job.Archive(T0.AddDays(1), 3).Should().BeFalse("version 3 is older than the last applied version 5");
        job.Status.Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public void Received_WithoutJobId_Throws()
    {
        var act = () => SyncJobStatus.Received(" ", Guid.NewGuid(), Partner, T0);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}

public class UsageWindowTests
{
    [Fact]
    [Trait("Story", "US-3.1.3-11")]
    [Trait("AC", "AC-02")]
    public void Create_EndBeforeStart_ThrowsInvalidFieldWithPublishedCode()
    {
        var act = () => UsageWindow.Create(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 1));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AuditRuleCodes.InvalidDateRange);
        ex.ExternalCode.Should().Be("E-TPJPRI-INVALID-FIELD");
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void Create_SingleDay_IsValid() => UsageWindow.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1)).To.Should().Be(new DateOnly(2026, 3, 1));

    [Fact]
    public void Create_MoreThanTwelveMonths_IsRefused()
    {
        var act = () => UsageWindow.Create(new DateOnly(2025, 1, 1), new DateOnly(2026, 3, 1));

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-TPJPRI-INVALID-FIELD");
    }

    [Fact]
    public void CountSubmission_Increments()
    {
        var counter = IntegrationUsageDaily.For(Guid.NewGuid(), new DateOnly(2026, 3, 1));
        counter.CountSubmission();
        counter.CountSubmission();

        counter.Submitted.Should().Be(2);
        counter.Matched.Should().Be(0);
    }
}

public class HistoryAndNotificationTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.2.4-02")]
    [Trait("AC", "AC-01")]
    public void JobStatusHistory_RecordsOldAndNewStatus()
    {
        var row = JobStatusHistoryEntry.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Draft", "Active", null, T0);

        row.FromStatus.Should().Be("Draft");
        row.ToStatus.Should().Be("Active");
        row.ChangedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void JobStatusHistory_InitialCreation_HasNoFromStatus() =>
        JobStatusHistoryEntry.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " ", "Draft", null, T0).FromStatus.Should().BeNull();

    [Fact]
    public void JobStatusHistory_WithoutEmployer_Throws()
    {
        var act = () => JobStatusHistoryEntry.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null, "Draft", null, T0);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void NotificationLog_UpdateStatus_IgnoresOlderUpdates()
    {
        var entry = NotificationLogEntry.Record(Guid.NewGuid(), Guid.NewGuid(), "Sms", "Otp", "+970****4567", null, "Sent", T0);

        entry.UpdateStatus("Delivered", T0.AddMinutes(5));
        entry.UpdateStatus("Sent", T0.AddMinutes(1));

        entry.Status.Should().Be("Delivered");
    }

    [Fact]
    [Trait("Story", "US-3.6.3-05")]
    [Trait("AC", "AC-03")]
    public void NotificationLog_UnmaskedEmail_IsRefused()
    {
        var act = () => NotificationLogEntry.Record(Guid.NewGuid(), null, "Email", "Welcome", "someone@example.com", "Hi", "Sent", T0);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.PiiInDetails);
    }

    [Fact]
    public void NotificationLog_MaskedEmail_IsAccepted() =>
        NotificationLogEntry.Record(Guid.NewGuid(), null, "Email", "Welcome", "s***@example.com", "Hi", "Sent", T0).MaskedRecipient.Should().Be("s***@example.com");
}

public class EmployerDashboardTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.1.2-07")]
    [Trait("AC", "AC-02")]
    public void Dashboard_TracksPostingsShortlistsAndRegistration()
    {
        var dashboard = EmployerDashboard.Open(Guid.NewGuid(), T0);
        var posting = Guid.NewGuid();

        dashboard.MarkRegistrationApproved(T0.AddMinutes(1));
        dashboard.TrackPosting(posting, "Engineer", "Draft", T0.AddMinutes(2));
        dashboard.TrackPosting(posting, null, "Active", T0.AddMinutes(3));
        dashboard.CountShortlist(T0.AddMinutes(4));

        dashboard.RegistrationApproved.Should().BeTrue();
        dashboard.Postings.Should().ContainSingle().Which.Should().Match<DashboardPosting>(p => p.Title == "Engineer" && p.Status == "Active");
        dashboard.ShortlistCount.Should().Be(1);
        dashboard.UpdatedAtUtc.Should().Be(T0.AddMinutes(4));
    }

    [Fact]
    public void TrackPosting_OlderUpdate_DoesNotOverwriteNewerStatus()
    {
        var dashboard = EmployerDashboard.Open(Guid.NewGuid(), T0);
        var posting = Guid.NewGuid();
        dashboard.TrackPosting(posting, "Engineer", "Closed", T0.AddMinutes(10));

        dashboard.TrackPosting(posting, null, "Active", T0.AddMinutes(5));

        dashboard.Postings.Single().Status.Should().Be("Closed");
    }

    [Fact]
    public void Open_WithoutEmployer_Throws()
    {
        var act = () => EmployerDashboard.Open(Guid.Empty, T0);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}

public class ExportJobTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Dictionary<string, string> Params = new() { ["year"] = "2026", ["region"] = "Gaza" };

    private static ExportJob NewJob() => ExportJob.Request(Guid.NewGuid(), ReportType.PostingsByRegion, ExportFormat.Csv, Params, T0);

    [Fact]
    public void Request_StartsQueuedAndInProgress()
    {
        var job = NewJob();

        job.Status.Should().Be(ExportStatus.Queued);
        job.IsInProgress.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-10")]
    [Trait("AC", "AC-03")]
    public void HashParameters_IsStableRegardlessOfParameterOrder()
    {
        var a = ExportJob.HashParameters(ReportType.PostingsByRegion, ExportFormat.Csv, new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var b = ExportJob.HashParameters(ReportType.PostingsByRegion, ExportFormat.Csv, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        var otherFormat = ExportJob.HashParameters(ReportType.PostingsByRegion, ExportFormat.Json, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        a.Should().Be(b);
        a.Should().NotBe(otherFormat);
    }

    [Fact]
    public void HappyPath_QueuedGeneratingReady()
    {
        var job = NewJob();
        job.StartGenerating();
        job.Status.Should().Be(ExportStatus.Generating);

        job.Complete("simulated://x", T0.AddMinutes(1));

        job.Status.Should().Be(ExportStatus.Ready);
        job.ResultRef.Should().Be("simulated://x");
        job.IsInProgress.Should().BeFalse();
        job.CompletedAtUtc.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void Fail_FromGenerating_RecordsReasonAndFinishes()
    {
        var job = NewJob();
        job.StartGenerating();

        job.Fail(new string('x', 900), T0);

        job.Status.Should().Be(ExportStatus.Failed);
        job.FailureReason!.Length.Should().Be(500);
    }

    [Fact]
    public void Transitions_OutOfOrder_AreRefused()
    {
        var job = NewJob();
        var completeQueued = () => job.Complete("x", T0);
        completeQueued.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.ExportInvalidTransition);

        job.StartGenerating();
        var startAgain = () => job.StartGenerating();
        startAgain.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.Conflict);

        job.Complete("x", T0);
        var failReady = () => job.Fail("late", T0);
        failReady.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Complete_WithoutResultRef_Throws()
    {
        var job = NewJob();
        job.StartGenerating();

        var act = () => job.Complete(" ", T0);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.InvalidEntry);
    }
}

public class CandidateInsightRecordTests
{
    private static readonly DateTime T0 = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Refresh_OlderComputation_IsIgnored()
    {
        var insight = CandidateInsightRecord.Compute(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Immediate", 1000m, 80m, Array.Empty<string>(), T0);

        insight.Refresh("Later", 2000m, 90m, new[] { "fitScore" }, T0.AddMinutes(-5));

        insight.Availability.Should().Be("Immediate");

        insight.Refresh("Later", 2000m, 90m, new[] { "fitScore" }, T0.AddMinutes(5));

        insight.Availability.Should().Be("Later");
        insight.WithheldFields.Should().ContainSingle("fitScore");
    }
}
