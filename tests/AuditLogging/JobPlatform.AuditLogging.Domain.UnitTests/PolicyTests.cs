using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain.UnitTests;

public class AccessScopePolicyTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    [Theory]
    [Trait("Story", "US-3.1.3-07")]
    [Trait("AC", "AC-02")]
    [InlineData(AuditCategory.ApiCall, ActorType.ExternalJobSite, true, true)]
    [InlineData(AuditCategory.ApiCall, ActorType.ExternalJobSite, false, false)] // another partner's entries
    [InlineData(AuditCategory.Submission, ActorType.ExternalJobSite, true, true)]
    [InlineData(AuditCategory.SyncError, ActorType.ExternalJobSite, false, false)]
    [InlineData(AuditCategory.ApiCall, ActorType.Employer, true, false)] // wrong actor type
    [InlineData(AuditCategory.ApiCall, ActorType.JobSeeker, true, false)]
    [InlineData(AuditCategory.JobStatus, ActorType.Employer, true, true)]
    [InlineData(AuditCategory.JobStatus, ActorType.Employer, false, false)]
    [InlineData(AuditCategory.Insight, ActorType.Employer, true, true)]
    [InlineData(AuditCategory.Insight, ActorType.ExternalJobSite, true, false)]
    [InlineData(AuditCategory.Notification, ActorType.JobSeeker, true, true)]
    [InlineData(AuditCategory.Notification, ActorType.JobSeeker, false, false)]
    [InlineData(AuditCategory.Notification, ActorType.Employer, true, true)] // the notification history is per user, whatever the actor kind
    public void CanView_OwnedCategories_AllowsOnlyTheMatchingOwner(AuditCategory category, ActorType actor, bool ownEntry, bool expected)
    {
        var ownerType = AccessScopePolicy.OwnerTypeFor(category)!.Value;
        var owner = OwnerScope.Of(ownerType, ownEntry ? Me : Other);

        var allowed = AccessScopePolicy.CanView(category, new Viewer(actor, Me), owner);

        allowed.Should().Be(expected);
    }

    [Theory]
    [InlineData(AuditCategory.Access)]
    [InlineData(AuditCategory.AdminAction)]
    [InlineData(AuditCategory.JobAudit)]
    [InlineData(AuditCategory.GovernmentExchange)]
    [InlineData(AuditCategory.Email)]
    [InlineData(AuditCategory.Sms)]
    [InlineData(AuditCategory.Profile)]
    public void CanView_AdministratorOnlyCategories_RefusesEveryoneButAdministrators(AuditCategory category)
    {
        AccessScopePolicy.CanView(category, new Viewer(ActorType.Administrator, Me), OwnerScope.AdminOnly).Should().BeTrue();
        foreach (var actor in new[] { ActorType.ExternalJobSite, ActorType.Employer, ActorType.JobSeeker, ActorType.Guest })
        {
            AccessScopePolicy.CanView(category, new Viewer(actor, Me), OwnerScope.AdminOnly).Should().BeFalse($"{actor} must not see {category}");
        }
    }

    [Fact]
    public void CanView_AnonymousViewer_IsAlwaysRefused()
    {
        AccessScopePolicy.CanView(AuditCategory.ApiCall, new Viewer(null, null), OwnerScope.Of(OwnerType.Partner, Me)).Should().BeFalse();
        AccessScopePolicy.CanView(AuditCategory.Access, new Viewer(ActorType.Administrator, null), OwnerScope.AdminOnly).Should().BeFalse();
    }

    [Theory]
    [InlineData(AuditCategory.ApiCall, "E-TPJPRI-FORBIDDEN")]
    [InlineData(AuditCategory.Submission, "E-TPJPRI-FORBIDDEN")]
    [InlineData(AuditCategory.SyncError, "E-TPJPRI-FORBIDDEN")]
    [InlineData(AuditCategory.JobStatus, "E-JST-FORBIDDEN")]
    [InlineData(AuditCategory.Notification, "E-INAPPN-FORBIDDEN")]
    [InlineData(AuditCategory.Access, "E-AAFR-FORBIDDEN")]
    [InlineData(AuditCategory.GovernmentExchange, "E-GDI-FORBIDDEN")]
    [InlineData(AuditCategory.Email, "E-EMAILN-FORBIDDEN")]
    [InlineData(AuditCategory.Sms, "E-SMSN-FORBIDDEN")]
    [InlineData(AuditCategory.AdminAction, "E-AUM-FORBIDDEN")]
    [InlineData(AuditCategory.JobAudit, "E-AUM-FORBIDDEN")]
    public void EnsureCanView_WhenRefused_ThrowsTheCategorysForbiddenCode(AuditCategory category, string code)
    {
        var act = () => AccessScopePolicy.EnsureCanView(category, new Viewer(ActorType.Guest, Guid.NewGuid()), OwnerScope.AdminOnly);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.ExternalCode.Should().Be(code);
        ex.Kind.Should().Be(BusinessRuleKind.Forbidden);
    }

    [Fact]
    public void EnsureCanView_OwnedCategoryOfSomeoneElse_ReportsNotOwner()
    {
        var act = () => AccessScopePolicy.EnsureCanView(AuditCategory.ApiCall, new Viewer(ActorType.ExternalJobSite, Me), OwnerScope.Of(OwnerType.Partner, Other));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.NotOwner);
    }
}

public class RetentionPolicyTests
{
    private static readonly DateTime Occurred = new(2026, 1, 31, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.1.3-06")]
    [Trait("AC", "AC-04")]
    public void RetainUntil_DefaultIsTwelveMonths() => new RetentionPolicy().RetainUntil(Occurred).Should().Be(Occurred.AddMonths(12));

    [Fact]
    public void IsExpired_ExactlyAtTheBoundary_IsStillLive()
    {
        var policy = new RetentionPolicy();
        var boundary = policy.RetainUntil(Occurred);

        policy.IsExpired(boundary, boundary).Should().BeFalse();
        policy.IsExpired(boundary, boundary.AddTicks(1)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    public void Constructor_OutOfRange_Throws(int months)
    {
        var act = () => new RetentionPolicy(months);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.RetentionInvalid);
    }
}

public class CandidateInsightPolicyTests
{
    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-02")]
    public void Fields_WhenWithheld_ReportUnavailableNotADefault()
    {
        var withheld = new[] { "availability", "expectedSalary" };

        CandidateInsightPolicy.Availability("Immediate", withheld).Should().Be("unavailable");
        CandidateInsightPolicy.ExpectedSalary(1500m, withheld).Should().Be("unavailable");
        CandidateInsightPolicy.Fit(87.5m, withheld).Should().Be("87.5");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-03")]
    public void Fields_WhenNotDisclosed_ReportUnavailable()
    {
        CandidateInsightPolicy.Availability(null, Array.Empty<string>()).Should().Be("unavailable");
        CandidateInsightPolicy.ExpectedSalary(null, Array.Empty<string>()).Should().Be("unavailable");
        CandidateInsightPolicy.Fit(null, Array.Empty<string>()).Should().Be("unavailable");
    }
}
