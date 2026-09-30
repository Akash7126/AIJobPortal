using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Infrastructure.Persistence;
using JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.IntegrationTests;

internal static class Db
{
    public static SqliteTestDatabase<AuditDbContext> New() => new(o => new AuditDbContext(o));

    public static readonly RetentionPolicy Retention = new();
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static AuditEntry Entry(AuditCategory category = AuditCategory.Access, Guid? owner = null, string subject = "S-1", DateTime? at = null,
        AuditOutcome outcome = AuditOutcome.Success, string bc = "account-identity", Guid? messageId = null, OwnerType type = OwnerType.Partner) =>
        AuditEntry.Record(bc, messageId ?? Guid.NewGuid(), category, at ?? T0, null, null, "Subject", subject,
            owner is { } o ? OwnerScope.Of(type, o) : OwnerScope.AdminOnly, "Act", outcome, null, new Dictionary<string, string> { ["k"] = "v" }, Retention);
}

public class RepositoryTests
{
    [Fact]
    public async Task AuditEntry_IsPersisted_AndRoundTripsEveryField()
    {
        await using var database = Db.New();
        var owner = Guid.NewGuid();
        var entry = Db.Entry(AuditCategory.ApiCall, owner);
        await using (var write = database.NewContext())
        {
            write.AuditEntries.Add(entry);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await read.AuditEntries.SingleAsync();
        loaded.Category.Should().Be(AuditCategory.ApiCall);
        loaded.OwnerType.Should().Be(OwnerType.Partner);
        loaded.OwnerId.Should().Be(owner);
        loaded.OccurredAtUtc.Should().Be(Db.T0);
        loaded.OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        loaded.RetainUntilUtc.Should().Be(Db.T0.AddMonths(12));
        loaded.Details["k"].Should().Be("v");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-05")]
    [Trait("AC", "AC-04")]
    public async Task AuditEntry_SameSourceMessageAndCategory_ViolatesTheUniqueIndex_ButOtherCategoriesMayShareTheMessage()
    {
        await using var database = Db.New();
        var message = Guid.NewGuid();
        await using var write = database.NewContext();
        write.AuditEntries.Add(Db.Entry(AuditCategory.Submission, Guid.NewGuid(), messageId: message));
        write.AuditEntries.Add(Db.Entry(AuditCategory.JobAudit, messageId: message));
        await write.SaveChangesAsync();

        write.AuditEntries.Add(Db.Entry(AuditCategory.Submission, Guid.NewGuid(), messageId: message));
        var act = () => write.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task EntryRepository_ExistsAsync_And_ListExpiredAsync()
    {
        await using var database = Db.New();
        var old = Db.Entry(at: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var recent = Db.Entry(at: Db.T0);
        await using (var write = database.NewContext())
        {
            write.AuditEntries.AddRange(old, recent);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var repository = new AuditEntryRepository(read);
        (await repository.ExistsAsync(old.SourceBc, old.SourceMessageId, old.Category)).Should().BeTrue();
        (await repository.ExistsAsync(old.SourceBc, old.SourceMessageId, AuditCategory.Email)).Should().BeFalse();
        (await repository.ListExpiredAsync(Db.T0, 10)).Should().ContainSingle().Which.Id.Should().Be(old.Id);
    }

    [Fact]
    public async Task SyncJobStatus_IsKeyedByPlatformJobId()
    {
        await using var database = Db.New();
        var partner = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var status = SyncJobStatus.Received("PJ-1", Guid.NewGuid(), partner, Db.T0);
            status.MarkSynced(Db.T0, 1);
            write.SyncJobStatuses.Add(status);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new SyncJobStatusRepository(read).GetAsync("PJ-1");
        loaded!.Status.Should().Be(SyncStatus.Synced);
        loaded.OwnerId.Should().Be(partner);
        (await new SyncJobStatusRepository(read).GetAsync("nope")).Should().BeNull();
    }

    [Fact]
    public async Task UsageCounter_IsUniquePerPartnerAndDay()
    {
        await using var database = Db.New();
        var partner = Guid.NewGuid();
        var day = new DateOnly(2026, 4, 1);
        await using var write = database.NewContext();
        write.IntegrationUsageDaily.Add(IntegrationUsageDaily.For(partner, day));
        await write.SaveChangesAsync();

        (await new UsageCounterRepository(write).GetAsync(partner, day))!.Day.Should().Be(day);

        write.IntegrationUsageDaily.Add(IntegrationUsageDaily.For(partner, day));
        var act = () => write.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task JobStatusHistory_SourceMessageIsUnique()
    {
        await using var database = Db.New();
        var message = Guid.NewGuid();
        await using var write = database.NewContext();
        write.JobStatusHistory.Add(JobStatusHistoryEntry.Record(message, Guid.NewGuid(), Guid.NewGuid(), null, "Draft", null, Db.T0));
        await write.SaveChangesAsync();

        (await new JobStatusHistoryRepository(write).ExistsAsync(message)).Should().BeTrue();
        write.JobStatusHistory.Add(JobStatusHistoryEntry.Record(message, Guid.NewGuid(), Guid.NewGuid(), null, "Draft", null, Db.T0));
        var act = () => write.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task EmployerDashboard_PersistsItsPostings()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var dashboard = EmployerDashboard.Open(employer, Db.T0);
            dashboard.TrackPosting(Guid.NewGuid(), "Engineer", "Active", Db.T0);
            dashboard.TrackPosting(Guid.NewGuid(), "Analyst", "Draft", Db.T0);
            dashboard.CountShortlist(Db.T0);
            write.EmployerDashboards.Add(dashboard);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new EmployerDashboardRepository(read).GetAsync(employer);
        loaded!.Postings.Should().HaveCount(2);
        loaded.ShortlistCount.Should().Be(1);
    }

    [Fact]
    public async Task ExportJob_OnlyOneInProgressPerAdministratorTypeAndParameters()
    {
        await using var database = Db.New();
        var admin = Guid.NewGuid();
        var parameters = new Dictionary<string, string> { ["year"] = "2026" };
        await using var write = database.NewContext();
        var first = ExportJob.Request(admin, ReportType.TopSearches, ExportFormat.Csv, parameters, Db.T0);
        write.ExportJobs.Add(first);
        await write.SaveChangesAsync();

        var repository = new ExportJobRepository(write);
        (await repository.FindInProgressAsync(admin, ReportType.TopSearches, first.ParametersHash))!.Id.Should().Be(first.Id);
        (await repository.ListQueuedAsync(10)).Should().ContainSingle();

        write.ExportJobs.Add(ExportJob.Request(admin, ReportType.TopSearches, ExportFormat.Csv, parameters, Db.T0));
        var act = () => write.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>("the filtered unique index is the final guard for INV-04");
    }

    [Fact]
    public async Task ExportJob_AfterCompletion_AnIdenticalRequestIsAllowedAgain()
    {
        await using var database = Db.New();
        var admin = Guid.NewGuid();
        var parameters = new Dictionary<string, string>();
        await using var write = database.NewContext();
        var first = ExportJob.Request(admin, ReportType.TopSearches, ExportFormat.Csv, parameters, Db.T0);
        write.ExportJobs.Add(first);
        await write.SaveChangesAsync();
        first.StartGenerating();
        first.Complete("ref", Db.T0);
        await write.SaveChangesAsync();

        write.ExportJobs.Add(ExportJob.Request(admin, ReportType.TopSearches, ExportFormat.Csv, parameters, Db.T0));
        var act = () => write.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExportJob_ConcurrencyToken_DetectsALostUpdate()
    {
        await using var database = Db.New();
        var job = ExportJob.Request(Guid.NewGuid(), ReportType.TopSearches, ExportFormat.Csv, new Dictionary<string, string>(), Db.T0);
        await using (var seed = database.NewContext())
        {
            seed.ExportJobs.Add(job);
            await seed.SaveChangesAsync();
        }

        await using var a = database.NewContext();
        await using var b = database.NewContext();
        var fromA = await a.ExportJobs.SingleAsync();
        var fromB = await b.ExportJobs.SingleAsync();
        fromA.StartGenerating();
        await a.SaveChangesAsync();
        fromB.StartGenerating();

        var act = () => b.SaveChangesAsync();

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }
}

public class ReadStoreTests
{
    private static async Task<(SqliteTestDatabase<AuditDbContext> Db, Guid Partner)> SeedAsync()
    {
        var database = Db.New();
        var partner = Guid.NewGuid();
        await using var write = database.NewContext();
        write.AuditEntries.AddRange(
            Db.Entry(AuditCategory.ApiCall, partner, "req-1", Db.T0, AuditOutcome.Success),
            Db.Entry(AuditCategory.ApiCall, partner, "req-2", Db.T0.AddHours(1), AuditOutcome.Failure),
            Db.Entry(AuditCategory.ApiCall, partner, "req-3", Db.T0.AddHours(2), AuditOutcome.Failure),
            Db.Entry(AuditCategory.ApiCall, Guid.NewGuid(), "req-x", Db.T0, AuditOutcome.Success),
            Db.Entry(AuditCategory.Access, null, "acc-1", Db.T0));
        var archived = Db.Entry(AuditCategory.ApiCall, partner, "req-old", Db.T0.AddYears(-3));
        archived.Archive(Db.T0);
        write.AuditEntries.Add(archived);
        await write.SaveChangesAsync();
        return (database, partner);
    }

    [Fact]
    public async Task ListEntries_FiltersByCategoryOwnerAndOutcome_NewestFirst_HidingArchived()
    {
        var (database, partner) = await SeedAsync();
        await using var _ = database;
        await using var read = database.NewContext();
        var store = new AuditReadStore(read);

        var all = await store.ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, partner), new PageRequest());
        all.TotalCount.Should().Be(3);
        all.Items.Select(i => i.SubjectId).Should().Equal("req-3", "req-2", "req-1");

        var failures = await store.ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, partner, Outcome: AuditOutcome.Failure), new PageRequest());
        failures.Items.Should().HaveCount(2);

        var withArchived = await store.ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, partner, IncludeArchived: true), new PageRequest());
        withArchived.TotalCount.Should().Be(4);
        withArchived.Items.Single(i => i.SubjectId == "req-old").IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ListEntries_FiltersByTimeWindowAndSubject_AndPages()
    {
        var (database, partner) = await SeedAsync();
        await using var _ = database;
        await using var read = database.NewContext();
        var store = new AuditReadStore(read);

        var window = await store.ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, partner, FromUtc: Db.T0.AddMinutes(30), ToUtc: Db.T0.AddHours(1).AddMinutes(30)), new PageRequest());
        window.Items.Should().ContainSingle().Which.SubjectId.Should().Be("req-2");

        var bySubject = await store.ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, partner, "req-1"), new PageRequest());
        bySubject.Items.Should().ContainSingle();

        var paged = await store.ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, partner), new PageRequest(2, 2));
        paged.Items.Should().ContainSingle().Which.SubjectId.Should().Be("req-1");
        paged.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task ListEntries_NeverLeaksAnotherOwnersRows()
    {
        var (database, _) = await SeedAsync();
        await using var _ = database;
        await using var read = database.NewContext();

        var result = await new AuditReadStore(read).ListEntriesAsync(new EntryFilter(new[] { AuditCategory.ApiCall }, Guid.NewGuid()), new PageRequest());

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncDashboard_CountsByStatusForTheOwnerOnly()
    {
        await using var database = Db.New();
        var partner = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var synced = SyncJobStatus.Received("A", Guid.NewGuid(), partner, Db.T0);
            synced.MarkSynced(Db.T0, 1);
            var failed = SyncJobStatus.Received("B", Guid.NewGuid(), partner, Db.T0.AddMinutes(1));
            failed.MarkFailed("E-X", Db.T0.AddMinutes(1));
            var pending = SyncJobStatus.Received("C", Guid.NewGuid(), partner, Db.T0.AddMinutes(2));
            var other = SyncJobStatus.Received("D", Guid.NewGuid(), Guid.NewGuid(), Db.T0);
            write.SyncJobStatuses.AddRange(synced, failed, pending, other);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new AuditReadStore(read);
        var dashboard = await store.GetSyncDashboardAsync(partner, new PageRequest());
        (dashboard.Synced, dashboard.Failed, dashboard.Pending, dashboard.Archived).Should().Be((1, 1, 1, 0));
        dashboard.Jobs.TotalCount.Should().Be(3);
        dashboard.Jobs.Items.Select(j => j.PlatformJobId).Should().Equal("C", "B", "A");

        var status = await store.GetIntegrationStatusAsync(partner, new DateOnly(2026, 4, 1));
        status.Health.Should().Be("Degraded");
        (await store.GetIntegrationStatusAsync(Guid.NewGuid(), new DateOnly(2026, 4, 1))).Health.Should().Be("Idle");
    }

    [Fact]
    public async Task Usage_SumsTheWindowAndLast30DaysForThePartner()
    {
        await using var database = Db.New();
        var partner = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            foreach (var day in new[] { new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 25) })
            {
                var counter = IntegrationUsageDaily.For(partner, day);
                counter.CountSubmission();
                counter.CountSubmission();
                write.IntegrationUsageDaily.Add(counter);
            }

            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new AuditReadStore(read);
        var usage = await store.GetUsageAsync(partner, UsageWindow.Create(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 31)));
        usage.Submitted.Should().Be(4);
        usage.Days.Should().HaveCount(2);
        (await store.GetIntegrationStatusAsync(partner, new DateOnly(2026, 3, 31))).SubmittedLast30Days.Should().Be(6);
        (await store.GetUsageAsync(Guid.NewGuid(), UsageWindow.Create(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)))).Submitted.Should().Be(0);
    }

    [Fact]
    public async Task StatusHistory_ReturnsRowsOldestFirst_WithTheOwner()
    {
        await using var database = Db.New();
        var job = Guid.NewGuid();
        var employer = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.JobStatusHistory.AddRange(
                JobStatusHistoryEntry.Record(Guid.NewGuid(), job, employer, "Draft", "Active", null, Db.T0.AddHours(1)),
                JobStatusHistoryEntry.Record(Guid.NewGuid(), job, employer, null, "Draft", null, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var view = await new AuditReadStore(read).GetJobStatusHistoryAsync(job);
        view.EmployerId.Should().Be(employer);
        view.Rows.Select(r => r.ToStatus).Should().Equal("Draft", "Active");
        (await new AuditReadStore(read).GetJobStatusHistoryAsync(Guid.NewGuid())).EmployerId.Should().BeNull();
    }

    [Fact]
    public async Task NotificationLog_FiltersByChannelAndRecipient_NewestFirst()
    {
        await using var database = Db.New();
        var user = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.NotificationLog.AddRange(
                NotificationLogEntry.Record(Guid.NewGuid(), user, "Email", "Welcome", "s***@x.com", "Hi", "Sent", Db.T0),
                NotificationLogEntry.Record(Guid.NewGuid(), user, "Sms", "Otp", "+970****4567", null, "Sent", Db.T0.AddMinutes(1)),
                NotificationLogEntry.Record(Guid.NewGuid(), Guid.NewGuid(), "Email", "Welcome", "o***@x.com", "Hi", "Sent", Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new AuditReadStore(read);
        (await store.ListNotificationLogAsync(new[] { "Email" }, null, new PageRequest())).TotalCount.Should().Be(2);
        var mine = await store.ListNotificationLogAsync(new[] { "Email", "Sms" }, user, new PageRequest());
        mine.Items.Select(i => i.Channel).Should().Equal("Sms", "Email");
    }

    [Fact]
    public async Task EmployerDashboard_And_Insight_AreReadAsDtos()
    {
        await using var database = Db.New();
        var employer = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var job = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var dashboard = EmployerDashboard.Open(employer, Db.T0);
            dashboard.TrackPosting(job, "Engineer", "Active", Db.T0);
            dashboard.TrackPosting(Guid.NewGuid(), "Analyst", "Draft", Db.T0);
            write.EmployerDashboards.Add(dashboard);
            write.CandidateInsights.Add(CandidateInsightRecord.Compute(Guid.NewGuid(), job, employer, candidate, "Immediate", 1200m, 75.5m, new[] { "expectedSalary" }, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new AuditReadStore(read);
        var view = await store.GetEmployerDashboardAsync(employer);
        (view!.Postings, view.ActivePostings).Should().Be((2, 1));
        (await store.GetEmployerDashboardAsync(Guid.NewGuid())).Should().BeNull();

        var insight = await store.GetCandidateInsightAsync(candidate, job);
        insight!.EmployerId.Should().Be(employer);
        insight.FitScore.Should().Be(75.5m);
        insight.Withheld.Should().ContainSingle("expectedSalary");
        (await store.GetCandidateInsightAsync(candidate, Guid.NewGuid())).Should().BeNull();
    }
}
