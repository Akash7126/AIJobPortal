using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain.UnitTests;

public class JobDataMappingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static MappingRule[] ValidRules() => new[]
    {
        new MappingRule("job_title", "title", MappingTransform.Trim),
        new MappingRule("job_summary", "summary", MappingTransform.None),
        new MappingRule("skill_list", "skills", MappingTransform.SplitComma)
    };

    [Fact]
    [Trait("Story", "US-3.1.3-04")]
    [Trait("AC", "AC-01")]
    public void Create_WithFullCoverage_Succeeds()
    {
        var mapping = JobDataMapping.Create(Guid.NewGuid(), Guid.NewGuid(), ValidRules(), "v1", Guid.NewGuid(), At);

        mapping.MappingVersion.Should().Be(1);
        mapping.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobDataMappingUpdatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-04")]
    [Trait("AC", "AC-02")]
    public void Create_MissingRequiredTargetField_ThrowsRequiredFieldUnmapped()
    {
        var rules = new[] { new MappingRule("job_title", "title", MappingTransform.None) };

        var act = () => JobDataMapping.Create(Guid.NewGuid(), Guid.NewGuid(), rules, "v1", Guid.NewGuid(), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.MappingRequiredFieldUnmapped);
        ex.ExternalCode.Should().Be(ErrorCodes.MappingRequiredFieldUnmapped);
    }

    [Fact]
    [Trait("Story", "US-3.4.1-03")]
    [Trait("AC", "AC-01")]
    public void Configure_IncrementsVersionAndRaisesEvent()
    {
        var mapping = JobDataMapping.Create(Guid.NewGuid(), Guid.NewGuid(), ValidRules(), "v1", Guid.NewGuid(), At);
        mapping.ClearDomainEvents();

        mapping.Configure(ValidRules(), Guid.NewGuid(), At.AddDays(1));

        mapping.MappingVersion.Should().Be(2);
        mapping.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.4.1-03")]
    [Trait("AC", "AC-02")]
    public void Standardize_ConformingPayload_ProducesStandardJob()
    {
        var mapping = JobDataMapping.Create(Guid.NewGuid(), Guid.NewGuid(), ValidRules(), "v1", Guid.NewGuid(), At);
        var raw = new Dictionary<string, string> { ["job_title"] = " Backend Engineer ", ["job_summary"] = "Build things.", ["skill_list"] = "C#, SQL" };

        var standardJob = mapping.Standardize(raw);

        standardJob.Title.Should().Be("Backend Engineer");
        standardJob.Skills.Should().BeEquivalentTo(new[] { "C#", "SQL" });
    }

    [Fact]
    [Trait("Story", "US-3.4.1-03")]
    [Trait("AC", "AC-02")]
    public void Standardize_NonConformingPayload_ThrowsInvalidField()
    {
        var mapping = JobDataMapping.Create(Guid.NewGuid(), Guid.NewGuid(), ValidRules(), "v1", Guid.NewGuid(), At);
        var raw = new Dictionary<string, string> { ["job_title"] = "Backend Engineer" };

        var act = () => mapping.Standardize(raw);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.MappingNonConformingSource);
        ex.ExternalCode.Should().Be(ErrorCodes.MappingNonConformingSource);
    }
}
