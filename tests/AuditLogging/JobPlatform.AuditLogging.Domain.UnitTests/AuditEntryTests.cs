using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain.UnitTests;

public class AuditEntryTests
{
    private static readonly DateTime At = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly RetentionPolicy Retention = new();

    private static AuditEntry NewEntry(IReadOnlyDictionary<string, string>? details = null, OwnerScope? scope = null) =>
        AuditEntry.Record("external-integration", Guid.NewGuid(), AuditCategory.Submission, At, Guid.NewGuid(), "ExternalJobSite", "Job", "P-1",
            scope ?? OwnerScope.AdminOnly, "JobImported", AuditOutcome.Success, null, details, Retention);

    [Fact]
    public void Record_WithValidData_CreatesImmutableEntryRetainedFor12Months()
    {
        var partner = Guid.NewGuid();
        var entry = NewEntry(new Dictionary<string, string> { ["platformJobId"] = "P-1" }, OwnerScope.Of(OwnerType.Partner, partner));

        entry.Id.Should().NotBeEmpty();
        entry.Category.Should().Be(AuditCategory.Submission);
        entry.OwnerType.Should().Be(OwnerType.Partner);
        entry.OwnerId.Should().Be(partner);
        entry.RetainUntilUtc.Should().Be(At.AddMonths(12));
        entry.IsArchived.Should().BeFalse();
        entry.Details["platformJobId"].Should().Be("P-1");
    }

    [Theory]
    [InlineData("email")]
    [InlineData("recipientEmail")]
    [InlineData("mobilePhone")]
    [InlineData("password")]
    [InlineData("accessToken")]
    [InlineData("otpCode")]
    [InlineData("messageBody")]
    [InlineData("fullName")]
    public void Record_WithPersonalDataKey_ThrowsNoPiiInDetails(string key)
    {
        var act = () => NewEntry(new Dictionary<string, string> { [key] = "x" });

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AuditRuleCodes.PiiInDetails);
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Theory]
    [InlineData("someone@example.com")]
    [InlineData("970591234567")]
    public void Record_WithPersonalDataValue_ThrowsNoPiiInDetails(string value)
    {
        var act = () => NewEntry(new Dictionary<string, string> { ["note"] = value });

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.PiiInDetails);
    }

    [Fact]
    public void Record_WithGuidContainingLongDigitRun_IsNotMistakenForPersonalData()
    {
        var act = () => NewEntry(new Dictionary<string, string> { ["jobId"] = "a1b2c3d4-e5f6-7890-1234-567890123456" });

        act.Should().NotThrow();
    }

    [Fact]
    public void Record_WithoutSourceMessageId_Throws()
    {
        var act = () => AuditEntry.Record("bc", Guid.Empty, AuditCategory.Access, At, null, null, "Account", "1", OwnerScope.AdminOnly, "x", AuditOutcome.Success,
            null, null, Retention);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AuditRuleCodes.InvalidEntry);
    }

    [Fact]
    public void Archive_MarksEntryOnceAndKeepsIt()
    {
        var entry = NewEntry();
        entry.Archive(At.AddMonths(13));
        var first = entry.ArchivedAtUtc;
        entry.Archive(At.AddMonths(14));

        entry.IsArchived.Should().BeTrue();
        entry.ArchivedAtUtc.Should().Be(first);
    }

    [Fact]
    public void Sanitize_DropsViolatingEntriesAndCountsThem()
    {
        var clean = DetailsPolicy.Sanitize(new Dictionary<string, string> { ["email"] = "a@b.co", ["code"] = "E-1", ["phone"] = "1" }, out var removed);

        removed.Should().Be(2);
        clean.Should().ContainKey("code").And.NotContainKey("email");
    }
}

public class OwnerScopeTests
{
    [Fact]
    public void Parse_OwnedScope_ReturnsTypeAndId()
    {
        var id = Guid.NewGuid();

        var scope = OwnerScope.Parse($"Partner:{id}");

        scope.Type.Should().Be(OwnerType.Partner);
        scope.OwnerId.Should().Be(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AdminOnly")]
    [InlineData("Partner:not-a-guid")]
    [InlineData("Nobody:00000000-0000-0000-0000-000000000001")]
    [InlineData("AdminOnly:00000000-0000-0000-0000-000000000001")]
    public void Parse_UnknownOrMalformed_FailsClosedToAdminOnly(string? text) =>
        OwnerScope.Parse(text).Should().Be(OwnerScope.AdminOnly);

    [Fact]
    public void Of_WithoutOwnerId_Throws()
    {
        var act = () => OwnerScope.Of(OwnerType.Employer, Guid.Empty);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
