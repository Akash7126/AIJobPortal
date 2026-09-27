using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class CronExpressionTests
{
    [Fact]
    public void TryParse_ValidExpression_Succeeds()
    {
        var ok = CronExpression.TryParse("30 8 * * 1-5", out var cron);

        ok.Should().BeTrue();
        cron.Should().NotBeNull();
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("60 8 * * *")]
    [InlineData("30 25 * * *")]
    [InlineData(null)]
    public void TryParse_InvalidExpression_Fails(string? text) =>
        CronExpression.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void NextAfter_FindsTheNextMatchingWeekdayMorning()
    {
        CronExpression.TryParse("30 8 * * 1-5", out var cron);
        var saturday = new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc); // Saturday

        var next = cron!.NextAfter(saturday);

        next.Should().NotBeNull();
        next!.Value.DayOfWeek.Should().Be(DayOfWeek.Monday);
        next.Value.Hour.Should().Be(8);
        next.Value.Minute.Should().Be(30);
    }

    [Fact]
    public void NextAfter_SameDayLaterHour_ReturnsToday()
    {
        CronExpression.TryParse("0 14 * * *", out var cron);
        var morning = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

        var next = cron!.NextAfter(morning);

        next!.Value.Date.Should().Be(morning.Date);
        next.Value.Hour.Should().Be(14);
    }

    [Fact]
    public void NextAfter_PastTodaysTime_RollsToTomorrow()
    {
        CronExpression.TryParse("0 8 * * *", out var cron);
        var afterNoon = new DateTime(2026, 3, 2, 15, 0, 0, DateTimeKind.Utc);

        var next = cron!.NextAfter(afterNoon);

        next!.Value.Date.Should().Be(afterNoon.Date.AddDays(1));
    }
}

public class ReportScheduleTests
{
    private static readonly DateTime At = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc); // a Monday

    private static ReportSchedule Daily(Guid? templateId = null) =>
        ReportSchedule.Create("Daily activity", templateId ?? Guid.NewGuid(), null, ScheduleInterval.Daily, null, new[] { "admin@example.com" }, ReportFormat.Pdf,
            Guid.NewGuid(), At);

    [Fact]
    public void Create_Daily_SchedulesNextRunAtMidnight()
    {
        var schedule = Daily();

        schedule.NextRunAtUtc.Should().Be(At.Date.AddDays(1));
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_BothTemplateAndSavedReport_Throws()
    {
        var act = () => ReportSchedule.Create("X", Guid.NewGuid(), Guid.NewGuid(), ScheduleInterval.Daily, null, new[] { "a@b.com" }, ReportFormat.Pdf, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidScheduleInterval);
    }

    [Fact]
    public void Create_NeitherTemplateNorSavedReport_Throws()
    {
        var act = () => ReportSchedule.Create("X", null, null, ScheduleInterval.Daily, null, new[] { "a@b.com" }, ReportFormat.Pdf, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_NoRecipients_Throws()
    {
        var act = () => ReportSchedule.Create("X", Guid.NewGuid(), null, ScheduleInterval.Daily, null, Array.Empty<string>(), ReportFormat.Pdf, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_CronFiringMoreThanOncePerHour_Throws()
    {
        var act = () => ReportSchedule.Create("X", Guid.NewGuid(), null, ScheduleInterval.Cron, "*/5 * * * *", new[] { "a@b.com" }, ReportFormat.Pdf, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_ValidCron_Succeeds()
    {
        var schedule = ReportSchedule.Create("X", Guid.NewGuid(), null, ScheduleInterval.Cron, "0 6 * * *", new[] { "a@b.com" }, ReportFormat.Pdf, Guid.NewGuid(), At);

        schedule.Interval.Should().Be(ScheduleInterval.Cron);
    }

    [Fact]
    public void Create_RecipientsDeduplicatedAndLowercased()
    {
        var schedule = ReportSchedule.Create("X", Guid.NewGuid(), null, ScheduleInterval.Daily, null, new[] { "A@B.com", "a@b.com", "c@d.com" }, ReportFormat.Pdf,
            Guid.NewGuid(), At);

        schedule.Recipients.Should().Equal("a@b.com", "c@d.com");
    }

    [Fact]
    public void IsDue_BeforeNextRun_ReturnsFalse() =>
        Daily().IsDue(At).Should().BeFalse();

    [Fact]
    public void IsDue_AtOrAfterNextRun_ReturnsTrue() =>
        Daily().IsDue(At.Date.AddDays(1)).Should().BeTrue();

    [Fact]
    public void IsDue_Deactivated_ReturnsFalse()
    {
        var schedule = Daily();
        schedule.Deactivate();

        schedule.IsDue(At.Date.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void CompleteRun_WhenDue_RaisesDistributionRequestedAndAdvances()
    {
        var schedule = Daily();
        var due = schedule.NextRunAtUtc;

        schedule.CompleteRun("signed-ref-1", due);

        schedule.LastRunAtUtc.Should().Be(due);
        schedule.NextRunAtUtc.Should().Be(due.AddDays(1));
        var evt = schedule.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReportDistributionRequestedDomainEvent>().Which;
        evt.ReportRef.Should().Be("signed-ref-1");
        evt.Recipients.Should().Equal("admin@example.com");
    }

    [Fact]
    public void CompleteRun_WhenNotDue_Throws()
    {
        var schedule = Daily();

        var act = () => schedule.CompleteRun("ref", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    public void SkipRun_AdvancesWithoutRaisingAnEvent()
    {
        var schedule = Daily();
        var due = schedule.NextRunAtUtc;

        schedule.SkipRun(due);

        schedule.NextRunAtUtc.Should().Be(due.AddDays(1));
        schedule.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Weekly_FirstRunIsNextMonday()
    {
        var schedule = ReportSchedule.Create("W", Guid.NewGuid(), null, ScheduleInterval.Weekly, null, new[] { "a@b.com" }, ReportFormat.Csv, Guid.NewGuid(), At);

        schedule.NextRunAtUtc.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Monthly_FirstRunIsFirstOfNextMonth()
    {
        var schedule = ReportSchedule.Create("M", Guid.NewGuid(), null, ScheduleInterval.Monthly, null, new[] { "a@b.com" }, ReportFormat.Excel, Guid.NewGuid(), At);

        schedule.NextRunAtUtc.Should().Be(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
