using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Reporting.Application;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Infrastructure.Persistence;
using JobPlatform.Reporting.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    public static SqliteTestDatabase<ReportingDbContext> New() => new(o => new ReportingDbContext(o), new ReportingEventMapper());
}

public class FactStoreTests
{
    [Fact]
    public async Task FactEvent_RoundTrips_AndMessageIdIsUnique()
    {
        await using var database = Db.New();
        var messageId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var store = new FactStore(write);
            store.Add(FactEvent.Record(messageId, "BC-09", "JobPostingCreated", ActivityTypes.JobPosting, Db.T0, "Employer", "actor-key", null));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await read.FactEvents.AsNoTracking().SingleAsync();
        loaded.MessageId.Should().Be(messageId);
        loaded.SourceBc.Should().Be("BC-09");

        await using var dup = database.NewContext();
        var dupStore = new FactStore(dup);
        dupStore.Add(FactEvent.Record(messageId, "BC-09", "JobPostingCreated", ActivityTypes.JobPosting, Db.T0, "Employer", "actor-key", null));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task FactStore_EventExistsAsync_FindsAPersistedMessageId()
    {
        await using var database = Db.New();
        var messageId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            new FactStore(write).Add(FactEvent.Record(messageId, "BC-09", "JobPostingCreated", ActivityTypes.JobPosting, Db.T0, "Employer", "k", null));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new FactStore(read).EventExistsAsync(messageId)).Should().BeTrue();
        (await new FactStore(read).EventExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task GetOrOpenPostingAsync_ThenApplyDetails_RoundTripsPostingState()
    {
        await using var database = Db.New();
        var jobId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var store = new FactStore(write);
            var posting = await store.GetOrOpenPostingAsync(jobId, Db.T0);
            posting.ApplyDetails("active", "Backend Engineer", "software-development", "Ramallah", 1000, 2000, "Employer", Db.T0, version: 1);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await read.FactJobPostings.AsNoTracking().SingleAsync(p => p.Id == jobId);
        loaded.Status.Should().Be("active");
        loaded.Title.Should().Be("Backend Engineer");
        loaded.SalaryMin.Should().Be(1000);
        loaded.HasDetails.Should().BeTrue();

        // A second open within the same unit of work must find the row already tracked, not re-open it (FactStore.Local lookup).
        await using var second = database.NewContext();
        var secondStore = new FactStore(second);
        var reopened = await secondStore.GetOrOpenPostingAsync(jobId, Db.T0.AddDays(1));
        reopened.ApplyStatus("paused", Db.T0.AddDays(1), version: 2);
        var again = await secondStore.GetOrOpenPostingAsync(jobId, Db.T0.AddDays(2));
        ReferenceEquals(reopened, again).Should().BeTrue();
        await second.SaveChangesAsync();

        await using var final = database.NewContext();
        (await final.FactJobPostings.AsNoTracking().SingleAsync(p => p.Id == jobId)).Status.Should().Be("paused");
    }

    [Fact]
    public async Task FactSkillDemand_Unique_PerSkillSideAndSubject()
    {
        await using var database = Db.New();
        var subject = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            new FactStore(write).Add(FactSkillDemand.Of("C#", FactSkillDemand.Demand, subject, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        (await new FactStore(read).SkillFactExistsAsync("c#", FactSkillDemand.Demand, subject)).Should().BeTrue();

        await using var dup = database.NewContext();
        new FactStore(dup).Add(FactSkillDemand.Of("C#", FactSkillDemand.Demand, subject, Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task AddToDailyAsync_CreatesThenIncrements_TheSameCounter()
    {
        await using var database = Db.New();
        var day = DateOnly.FromDateTime(Db.T0);
        await using (var write = database.NewContext())
        {
            await new FactStore(write).AddToDailyAsync(day, "event.JobPostingCreated", 3);
            await write.SaveChangesAsync();
        }

        await using (var increment = database.NewContext())
        {
            await new FactStore(increment).AddToDailyAsync(day, "event.JobPostingCreated", 2);
            await increment.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var row = await read.AggDaily.AsNoTracking().SingleAsync(a => a.Day == day && a.Metric == "event.JobPostingCreated");
        row.Count.Should().Be(5);
    }

    [Fact]
    public async Task DeleteEventsBeforeAsync_RemovesOnlyEventsOlderThanCutoff()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var store = new FactStore(write);
            store.Add(FactEvent.Record(Guid.NewGuid(), "BC-09", "Old", ActivityTypes.JobPosting, Db.T0.AddMonths(-13), "Employer", "k1", null));
            store.Add(FactEvent.Record(Guid.NewGuid(), "BC-09", "New", ActivityTypes.JobPosting, Db.T0, "Employer", "k2", null));
            await write.SaveChangesAsync();
        }

        await using var delete = database.NewContext();
        var deleted = await new FactStore(delete).DeleteEventsBeforeAsync(Db.T0.AddMonths(-12), take: 100);

        deleted.Should().Be(1);
        await using var read = database.NewContext();
        (await read.FactEvents.AsNoTracking().SingleAsync()).EventType.Should().Be("New");
    }

    [Fact]
    public async Task RebuildRollupsAsync_RecomputesDailyCountersFromFactEvents()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var store = new FactStore(write);
            store.Add(FactEvent.Record(Guid.NewGuid(), "BC-09", "JobPostingCreated", ActivityTypes.JobPosting, Db.T0, "Employer", "k1", null));
            store.Add(FactEvent.Record(Guid.NewGuid(), "BC-09", "JobPostingCreated", ActivityTypes.JobPosting, Db.T0, "Employer", "k2", null));
            await write.SaveChangesAsync();
        }

        await using var rebuild = database.NewContext();
        var written = await new FactStore(rebuild).RebuildRollupsAsync();
        written.Should().Be(2); // one "event.*" and one "activity.*" counter
        await rebuild.SaveChangesAsync();

        await using var read = database.NewContext();
        var day = DateOnly.FromDateTime(Db.T0);
        (await read.AggDaily.AsNoTracking().SingleAsync(a => a.Metric == "event.JobPostingCreated" && a.Day == day)).Count.Should().Be(2);
    }
}

public class AnalyticsQueryServiceTests
{
    [Fact]
    public async Task EventsAsync_ReturnsOnlyEventsInTheRequestedWindow()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var store = new FactStore(write);
            store.Add(FactEvent.Record(Guid.NewGuid(), "BC-09", "InWindow", ActivityTypes.JobPosting, Db.T0, "Employer", "k1", null));
            store.Add(FactEvent.Record(Guid.NewGuid(), "BC-09", "OutOfWindow", ActivityTypes.JobPosting, Db.T0.AddDays(10), "Employer", "k2", null));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var rows = await new AnalyticsQueryService(read).EventsAsync(Db.T0.AddHours(-1), Db.T0.AddHours(1), max: 100);

        rows.Should().ContainSingle().Which.EventType.Should().Be("InWindow");
    }

    [Fact]
    public async Task DailyCountsAsync_FiltersByMetricPrefixAndDateRange()
    {
        await using var database = Db.New();
        var day = DateOnly.FromDateTime(Db.T0);
        await using (var write = database.NewContext())
        {
            write.AggDaily.Add(AggDaily.For(day, "activity.JobPosting", 4));
            write.AggDaily.Add(AggDaily.For(day, "event.JobPostingCreated", 4));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var rows = await new AnalyticsQueryService(read).DailyCountsAsync("activity.", day, day);

        rows.Should().ContainSingle().Which.Metric.Should().Be("activity.JobPosting");
    }
}

public class ConfigRepositoryTests
{
    [Fact]
    public async Task ReportTemplate_RoundTrips_ParametersAsJson()
    {
        await using var database = Db.New();
        var parameters = new List<TemplateParameter> { new("minSalary", ParameterType.Decimal, Min: "0", Default: "1000") };
        var templateId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var template = ReportTemplate.Create("Salary report", ReportDataSource.Employment, parameters, Guid.NewGuid(), Db.T0);
            templateId = template.Id;
            new ReportTemplateRepository(write).Add(template);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new ReportTemplateRepository(read).GetAsync(templateId);
        loaded!.Name.Should().Be("Salary report");
        loaded.Parameters.Should().ContainSingle().Which.Name.Should().Be("minSalary");
        loaded.Revision.Should().Be(1);
    }

    [Fact]
    public async Task ReportSchedule_CompleteRun_WritesAnOutboxRow_ForDistribution()
    {
        await using var database = Db.New();
        var schedule = ReportSchedule.Create("Weekly employment", Guid.NewGuid(), null, ScheduleInterval.Daily, null, new[] { "admin@example.com" },
            ReportFormat.Pdf, Guid.NewGuid(), Db.T0);
        var scheduleId = schedule.Id;

        await using var write = database.NewContext();
        new ReportScheduleRepository(write).Add(schedule);
        await write.SaveChangesAsync();

        schedule.CompleteRun("/api/v1/reports/shared/abc", schedule.NextRunAtUtc);
        await write.SaveChangesAsync();

        var outbox = await write.Set<OutboxMessage>().SingleAsync();
        outbox.Type.Should().Be("ReportDistributionRequested");

        await using var read = database.NewContext();
        var loaded = await new ReportScheduleRepository(read).GetAsync(scheduleId);
        loaded!.LastRunAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SavedReport_Save_RoundTripsDefinition_AndArchive()
    {
        await using var database = Db.New();
        var owner = Guid.NewGuid();
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "eventType", "events" }, Array.Empty<ReportFilter>(), Array.Empty<string>());
        var reportId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var report = SavedReport.Save(owner, "My activity report", definition, Db.T0);
            reportId = report.Id;
            new SavedReportRepository(write).Add(report);
            await write.SaveChangesAsync();
        }

        await using (var archive = database.NewContext())
        {
            var repo = new SavedReportRepository(archive);
            var loaded = await repo.GetAsync(reportId);
            loaded!.Definition.Fields.Should().Contain("eventType");
            loaded.Archive(Db.T0.AddDays(1));
            await archive.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var owned = await new SavedReportRepository(read).ListOwnedAsync(owner);
        owned.Should().BeEmpty("archived reports are excluded from the owner's active list");
    }

    [Fact]
    public async Task ReportExport_Request_EnforcesOneRunningExportPerAdministratorAndHash()
    {
        await using var database = Db.New();
        var administrator = Guid.NewGuid();
        var parameters = new Dictionary<string, string> { ["month"] = "2026-04" };

        await using (var write = database.NewContext())
        {
            var export = ReportExport.Request(administrator, ReportRefKind.LaborMarket, null, ReportFormat.Csv, parameters, Db.T0);
            new ReportExportRepository(write).Add(export);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var hash = ReportExport.HashParameters(ReportRefKind.LaborMarket, null, ReportFormat.Csv, parameters);
        var running = await new ReportExportRepository(read).FindRunningAsync(administrator, hash);
        running.Should().NotBeNull();

        await using var dup = database.NewContext();
        new ReportExportRepository(dup).Add(ReportExport.Request(administrator, ReportRefKind.LaborMarket, null, ReportFormat.Csv, parameters, Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task ReportExport_AddFile_RoundTripsTheGeneratedContent()
    {
        await using var database = Db.New();
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.LaborMarket, null, ReportFormat.Csv, new Dictionary<string, string>(), Db.T0);
        export.StartGenerating();
        export.Complete("/api/v1/admin/reports/exports/x/file", Db.T0);
        var exportId = export.Id;

        await using (var write = database.NewContext())
        {
            var repo = new ReportExportRepository(write);
            repo.Add(export);
            repo.AddFile(ReportExportFile.For(exportId, "report.csv", "text/csv", new byte[] { 1, 2, 3 }, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var file = await new ReportExportRepository(read).GetFileAsync(exportId);
        file!.FileName.Should().Be("report.csv");
        file.Content.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ReportAccessRule_RoundTrips_AndRoleIsUnique()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            new ReportAccessRuleRepository(write).Add(ReportAccessRule.Create("HrManager", new[] { ReportCategory.EmploymentStatistics }, Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new ReportAccessRuleRepository(read).GetByRoleAsync("HrManager");
        loaded!.IsAllowed(ReportCategory.EmploymentStatistics).Should().BeTrue();
        loaded.IsAllowed(ReportCategory.SystemPerformance).Should().BeFalse();

        await using var dup = database.NewContext();
        new ReportAccessRuleRepository(dup).Add(ReportAccessRule.Create("HrManager", new[] { ReportCategory.ActivityLogs }, Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task ReportAccessDecision_Record_WritesAnAuditOutboxRow()
    {
        await using var database = Db.New();
        await using var write = database.NewContext();
        new ReportAccessRuleRepository(write).AddDecision(ReportAccessDecision.Record(Guid.NewGuid(), ReportCategory.ActivityLogs, false, "GetUserActivityQuery", Db.T0));
        await write.SaveChangesAsync();

        var outbox = await write.Set<OutboxMessage>().SingleAsync();
        outbox.Type.Should().Be("AuditRecord");
    }

    [Fact]
    public async Task PerformanceAlertRule_Evaluate_RaisesAndPersistsAnAlert_WithOutboxRow()
    {
        await using var database = Db.New();
        var rule = PerformanceAlertRule.Create(PerformanceMetrics.ErrorRatePercent, AlertComparator.GreaterThan, 5m, 5, AlertSeverity.Critical, true, Db.T0);
        var ruleId = rule.Id;

        await using (var write = database.NewContext())
        {
            var repo = new PerformanceAlertRuleRepository(write);
            repo.Add(rule);
            var alert = rule.Evaluate(9m, Db.T0);
            alert.Should().NotBeNull();
            repo.AddAlert(alert!);
            await write.SaveChangesAsync();

            var outbox = await write.Set<OutboxMessage>().SingleAsync();
            outbox.Type.Should().Be("PerformanceAlertRaised");
        }

        await using var read = database.NewContext();
        var repository = new PerformanceAlertRuleRepository(read);
        (await repository.HasAlertSinceAsync(ruleId, Db.T0.AddMinutes(-1))).Should().BeTrue();
        (await repository.ListAlertsAsync(10)).Should().ContainSingle();
    }

    [Fact]
    public async Task LaborMarketReport_Generate_RoundTrips_AndPeriodIsUnique()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            new LaborMarketReportRepository(write).Add(LaborMarketReport.Generate("2026-03", "{\"postings\":10}", Guid.NewGuid(), Db.T0));
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new LaborMarketReportRepository(read).GetByPeriodAsync("2026-03");
        loaded!.ContentJson.Should().Contain("postings");

        await using var dup = database.NewContext();
        new LaborMarketReportRepository(dup).Add(LaborMarketReport.Generate("2026-03", "{}", Guid.NewGuid(), Db.T0));
        var act = () => dup.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task ActivityLogRetentionPolicy_RoundTrips_TheSingletonRow()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            var policy = ActivityLogRetentionPolicy.CreateDefault(6, Db.T0);
            policy.Set(18, Guid.NewGuid(), Db.T0);
            new RetentionPolicyRepository(write).Add(policy);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new RetentionPolicyRepository(read).GetAsync();
        loaded!.Id.Should().Be(ActivityLogRetentionPolicy.SingletonId);
        loaded.RetentionMonths.Should().Be(18);
    }
}
