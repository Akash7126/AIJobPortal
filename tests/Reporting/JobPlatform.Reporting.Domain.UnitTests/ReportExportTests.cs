using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class ReportExportTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyDictionary<string, string> Params = new Dictionary<string, string> { ["months"] = "12" };

    [Fact]
    public void HashParameters_SameParametersDifferentOrder_ProduceTheSameHash()
    {
        var a = new Dictionary<string, string> { ["x"] = "1", ["y"] = "2" };
        var b = new Dictionary<string, string> { ["y"] = "2", ["x"] = "1" };

        ReportExport.HashParameters(ReportRefKind.Template, Guid.Empty, ReportFormat.Pdf, a)
            .Should().Be(ReportExport.HashParameters(ReportRefKind.Template, Guid.Empty, ReportFormat.Pdf, b));
    }

    [Fact]
    public void HashParameters_DifferentFormat_ProducesDifferentHash()
    {
        ReportExport.HashParameters(ReportRefKind.Template, Guid.Empty, ReportFormat.Pdf, Params)
            .Should().NotBe(ReportExport.HashParameters(ReportRefKind.Template, Guid.Empty, ReportFormat.Csv, Params));
    }

    [Fact]
    public void Request_LaborMarketWithRefId_Throws()
    {
        var act = () => ReportExport.Request(Guid.NewGuid(), ReportRefKind.LaborMarket, Guid.NewGuid(), ReportFormat.Pdf, Params, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.ExportInvalidTransition);
    }

    [Fact]
    public void Request_NonLaborMarketWithoutRefId_Throws()
    {
        var act = () => ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, null, ReportFormat.Pdf, Params, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Request_Valid_StartsQueued()
    {
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, Guid.NewGuid(), ReportFormat.Pdf, Params, At);

        export.Status.Should().Be(ExportStatus.Queued);
        export.IsInProgress.Should().BeTrue();
        export.Parameters.Should().ContainKey("months").WhoseValue.Should().Be("12");
    }

    [Fact]
    public void FullLifecycle_QueuedToGeneratingToReady()
    {
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, Guid.NewGuid(), ReportFormat.Pdf, Params, At);

        export.StartGenerating();
        export.Status.Should().Be(ExportStatus.Generating);

        export.Complete("blob://export-1", At.AddSeconds(5));
        export.Status.Should().Be(ExportStatus.Ready);
        export.ResultRef.Should().Be("blob://export-1");
        export.IsInProgress.Should().BeFalse();
    }

    [Fact]
    public void StartGenerating_WhenNotQueued_Throws()
    {
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, Guid.NewGuid(), ReportFormat.Pdf, Params, At);
        export.StartGenerating();

        var act = () => export.StartGenerating();

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    public void Complete_WhenNotGenerating_Throws()
    {
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, Guid.NewGuid(), ReportFormat.Pdf, Params, At);

        var act = () => export.Complete("ref", At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Fail_WhileInProgress_SetsFailedAndTruncatesReason()
    {
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, Guid.NewGuid(), ReportFormat.Pdf, Params, At);

        export.Fail(new string('e', 600), At);

        export.Status.Should().Be(ExportStatus.Failed);
        export.FailureReason.Should().HaveLength(500);
    }

    [Fact]
    public void Fail_WhenAlreadyTerminal_Throws()
    {
        var export = ReportExport.Request(Guid.NewGuid(), ReportRefKind.Template, Guid.NewGuid(), ReportFormat.Pdf, Params, At);
        export.StartGenerating();
        export.Complete("ref", At);

        var act = () => export.Fail("boom", At);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}

public class ReportExportFileTests
{
    [Fact]
    public void For_UsesTheExportIdAsItsOwnId()
    {
        var exportId = Guid.NewGuid();

        var file = ReportExportFile.For(exportId, "report.pdf", "application/pdf", new byte[] { 1, 2, 3 }, DateTime.UtcNow);

        file.Id.Should().Be(exportId);
    }

    [Fact]
    public void For_EmptyContent_Throws()
    {
        var act = () => ReportExportFile.For(Guid.NewGuid(), "report.pdf", "application/pdf", Array.Empty<byte>(), DateTime.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
