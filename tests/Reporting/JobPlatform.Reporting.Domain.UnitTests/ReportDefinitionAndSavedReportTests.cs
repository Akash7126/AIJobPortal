using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class ReportDefinitionTests
{
    [Fact]
    public void ValidateAndNormalise_ValidFields_Passes()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "eventType", "events" }, Array.Empty<ReportFilter>(), Array.Empty<string>());

        var normalised = definition.ValidateAndNormalise();

        normalised.Fields.Should().Equal("eventType", "events");
        normalised.GroupBy.Should().Equal("eventType");
    }

    [Fact]
    public void ValidateAndNormalise_UnknownField_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "notAField" }, Array.Empty<ReportFilter>(), Array.Empty<string>());

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidDefinition);
    }

    [Fact]
    public void ValidateAndNormalise_DuplicateField_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "eventType", "eventType" }, Array.Empty<ReportFilter>(), Array.Empty<string>());

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateAndNormalise_TooManyFields_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, Array.Empty<string>(), Array.Empty<ReportFilter>(), Array.Empty<string>());

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateAndNormalise_MeasureWithoutGroupingAllDimensions_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "eventType", "sourceBc", "events" }, Array.Empty<ReportFilter>(), new[] { "eventType" });

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateAndNormalise_GroupByNonSelectedField_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "eventType" }, Array.Empty<ReportFilter>(), new[] { "sourceBc" });

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateAndNormalise_FilterOnMeasure_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "eventType", "events" },
            new[] { new ReportFilter("events", FilterOperator.Gte, "10") }, new[] { "eventType" });

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateAndNormalise_InvalidDateFilterValue_Throws()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "day", "events" },
            new[] { new ReportFilter("day", FilterOperator.Eq, "not-a-date") }, new[] { "day" });

        var act = () => definition.ValidateAndNormalise();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateAndNormalise_ValidDateFilter_Passes()
    {
        var definition = new ReportDefinition(ReportDataSource.Activity, new[] { "day", "events" },
            new[] { new ReportFilter("day", FilterOperator.Gte, "2026-01-01") }, new[] { "day" });

        definition.Invoking(d => d.ValidateAndNormalise()).Should().NotThrow();
    }
}

public class SavedReportTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static ReportDefinition Definition() =>
        new(ReportDataSource.Activity, new[] { "eventType", "events" }, Array.Empty<ReportFilter>(), Array.Empty<string>());

    [Fact]
    public void Save_SetsRetentionTwelveMonthsOut()
    {
        var report = SavedReport.Save(Guid.NewGuid(), "My report", Definition(), At);

        report.RetainUntilUtc.Should().Be(At.AddMonths(SavedReport.RetentionMonths));
        report.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Save_InvalidDefinition_Throws()
    {
        var invalid = new ReportDefinition(ReportDataSource.Activity, Array.Empty<string>(), Array.Empty<ReportFilter>(), Array.Empty<string>());

        var act = () => SavedReport.Save(Guid.NewGuid(), "X", invalid, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void EnsureOwnedBy_NonOwner_ThrowsForbidden()
    {
        var report = SavedReport.Save(Guid.NewGuid(), "X", Definition(), At);

        var act = () => report.EnsureOwnedBy(Guid.NewGuid());

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(ReportingRuleCodes.SavedReportNotOwner);
        ex.ExternalCode.Should().Be(ReportingErrorCodes.CustomForbidden);
    }

    [Fact]
    public void EnsureOwnedBy_Owner_DoesNotThrow()
    {
        var owner = Guid.NewGuid();
        var report = SavedReport.Save(owner, "X", Definition(), At);

        report.Invoking(r => r.EnsureOwnedBy(owner)).Should().NotThrow();
    }

    [Fact]
    public void Archive_SetsArchivedFlagAndTimestamp()
    {
        var report = SavedReport.Save(Guid.NewGuid(), "X", Definition(), At);

        report.Archive(At.AddMonths(12));

        report.IsArchived.Should().BeTrue();
        report.ArchivedAtUtc.Should().Be(At.AddMonths(12));
    }

    [Fact]
    public void Archive_Twice_IsIdempotent()
    {
        var report = SavedReport.Save(Guid.NewGuid(), "X", Definition(), At);
        report.Archive(At.AddMonths(12));

        report.Archive(At.AddMonths(13));

        report.ArchivedAtUtc.Should().Be(At.AddMonths(12));
    }
}
