using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class ReportAccessRuleTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_RoundTripsCategories()
    {
        var rule = ReportAccessRule.Create("Auditor", new[] { ReportCategory.ActivityLogs, ReportCategory.Custom }, At);

        rule.Categories.Should().BeEquivalentTo(new[] { ReportCategory.ActivityLogs, ReportCategory.Custom });
    }

    [Fact]
    public void IsAllowed_GrantedCategory_ReturnsTrue()
    {
        var rule = ReportAccessRule.Create("Auditor", new[] { ReportCategory.ActivityLogs }, At);

        rule.IsAllowed(ReportCategory.ActivityLogs).Should().BeTrue();
        rule.IsAllowed(ReportCategory.SystemPerformance).Should().BeFalse();
    }

    [Fact]
    public void Create_EmptyRole_Throws()
    {
        var act = () => ReportAccessRule.Create("", new[] { ReportCategory.Custom }, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidDefinition);
    }

    [Fact]
    public void Set_DedupesAndOrdersCategories()
    {
        var rule = ReportAccessRule.Create("Auditor", new[] { ReportCategory.Custom, ReportCategory.ActivityLogs, ReportCategory.Custom }, At);

        rule.Categories.Should().Equal(ReportCategory.ActivityLogs, ReportCategory.Custom);
    }
}

public class ReportAccessPolicyTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsAllowed_NoRulesForCallerRoles_DefaultsToAllowed() =>
        ReportAccessPolicy.IsAllowed(Array.Empty<ReportAccessRule>(), ReportCategory.SystemPerformance).Should().BeTrue();

    [Fact]
    public void IsAllowed_AtLeastOneRuleGrants_ReturnsTrue()
    {
        var rules = new[] { ReportAccessRule.Create("Role1", new[] { ReportCategory.ActivityLogs }, At) };

        ReportAccessPolicy.IsAllowed(rules, ReportCategory.ActivityLogs).Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_RulesExistButNoneGrantsIt_ReturnsFalse()
    {
        var rules = new[] { ReportAccessRule.Create("Role1", new[] { ReportCategory.ActivityLogs }, At) };

        ReportAccessPolicy.IsAllowed(rules, ReportCategory.Custom).Should().BeFalse();
    }
}

public class ReportAccessDecisionTests
{
    [Fact]
    public void Record_TruncatesLongRequestAndRaisesEvent()
    {
        var decision = ReportAccessDecision.Record(Guid.NewGuid(), ReportCategory.Custom, false, new string('r', 200), DateTime.UtcNow);

        decision.Request.Should().HaveLength(100);
        decision.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReportAccessDecidedDomainEvent>()
            .Which.Allowed.Should().BeFalse();
    }
}
