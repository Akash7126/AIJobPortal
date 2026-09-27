using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class TemplateParameterTests
{
    [Fact]
    public void Validate_WellFormedNumericParameter_Passes()
    {
        var parameter = new TemplateParameter("months", ParameterType.Int, "1", "24", "12");

        parameter.Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_MinGreaterThanMax_Throws()
    {
        var parameter = new TemplateParameter("months", ParameterType.Int, "24", "1");

        var act = () => parameter.Validate();

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidTemplateParameter);
    }

    [Fact]
    public void Validate_DefaultOutsideRange_Throws()
    {
        var parameter = new TemplateParameter("months", ParameterType.Int, "1", "10", "20");

        var act = () => parameter.Validate();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Validate_EnumWithoutOptions_Throws()
    {
        var parameter = new TemplateParameter("status", ParameterType.Enum);

        var act = () => parameter.Validate();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Validate_EnumDefaultNotInOptions_Throws()
    {
        var parameter = new TemplateParameter("status", ParameterType.Enum, Default: "closed", Options: new[] { "open", "pending" });

        var act = () => parameter.Validate();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Accepts_EnumValueInOptions_ReturnsTrue()
    {
        var parameter = new TemplateParameter("status", ParameterType.Enum, Options: new[] { "open", "closed" });

        parameter.Accepts("open").Should().BeTrue();
        parameter.Accepts("archived").Should().BeFalse();
    }

    [Fact]
    public void Accepts_NumberWithinBounds_ReturnsTrue()
    {
        var parameter = new TemplateParameter("months", ParameterType.Int, "1", "24");

        parameter.Accepts("12").Should().BeTrue();
        parameter.Accepts("30").Should().BeFalse();
        parameter.Accepts("not-a-number").Should().BeFalse();
    }

    [Fact]
    public void Accepts_NullValue_IsAlwaysAccepted()
    {
        var parameter = new TemplateParameter("months", ParameterType.Int, "1", "24");

        parameter.Accepts(null).Should().BeTrue();
    }
}

public class ReportTemplateTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<TemplateParameter> Params() => new[] { new TemplateParameter("months", ParameterType.Int, "1", "24", "12") };

    [Fact]
    public void Create_SetsRevisionToOne()
    {
        var template = ReportTemplate.Create("Monthly activity", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);

        template.Revision.Should().Be(1);
        template.Parameters.Should().ContainSingle().Which.Name.Should().Be("months");
    }

    [Fact]
    public void Save_LaterSaveWins_IncrementsRevision()
    {
        var template = ReportTemplate.Create("Monthly activity", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);

        template.Save("Renamed", ReportDataSource.Employment, Array.Empty<TemplateParameter>(), At.AddMinutes(1));

        template.Name.Should().Be("Renamed");
        template.DataSource.Should().Be(ReportDataSource.Employment);
        template.Revision.Should().Be(2);
    }

    [Fact]
    public void Save_DuplicateParameterNames_Throws()
    {
        var template = ReportTemplate.Create("T", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);
        var duplicated = new[] { new TemplateParameter("months", ParameterType.Int), new TemplateParameter("Months", ParameterType.Int) };

        var act = () => template.Save("T", ReportDataSource.Activity, duplicated, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Save_TooManyParameters_Throws()
    {
        var template = ReportTemplate.Create("T", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);
        var many = Enumerable.Range(0, 31).Select(i => new TemplateParameter($"p{i}", ParameterType.String)).ToArray();

        var act = () => template.Save("T", ReportDataSource.Activity, many, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ResolveArguments_MissingValue_UsesDeclaredDefault()
    {
        var template = ReportTemplate.Create("T", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);

        var resolved = template.ResolveArguments(null);

        resolved["months"].Should().Be("12");
    }

    [Fact]
    public void ResolveArguments_SuppliedValue_Overrides()
    {
        var template = ReportTemplate.Create("T", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);

        var resolved = template.ResolveArguments(new Dictionary<string, string> { ["months"] = "6" });

        resolved["months"].Should().Be("6");
    }

    [Fact]
    public void ResolveArguments_UndeclaredParameter_Throws()
    {
        var template = ReportTemplate.Create("T", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);

        var act = () => template.ResolveArguments(new Dictionary<string, string> { ["unknown"] = "x" });

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ResolveArguments_ValueOutsideDeclaredRange_Throws()
    {
        var template = ReportTemplate.Create("T", ReportDataSource.Activity, Params(), Guid.NewGuid(), At);

        var act = () => template.ResolveArguments(new Dictionary<string, string> { ["months"] = "999" });

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
