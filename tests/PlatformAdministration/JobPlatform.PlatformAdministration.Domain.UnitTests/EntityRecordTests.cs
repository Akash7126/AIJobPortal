using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.UnitTests;

public class EntityRecordTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static Dictionary<string, string> JobSeeker() => new() { ["fullName"] = "Layla Haddad", ["mobile"] = "+970 59 000 0001" };

    [Fact]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-01")]
    public void Create_JobSeeker_RaisesCreatedEventAndNormalisesKey()
    {
        var record = PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.JobSeeker, JobSeeker(), Admin, false, At);

        record.IdentityKey.Should().Be("+970590000001");
        record.Status.Should().Be(PlatformEntityRecordStatus.Created);
        record.CreatedBy.Should().Be(Admin.Id);
        record.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PlatformEntityRecordCreatedDomainEvent>()
            .Which.EntityType.Should().Be(PlatformEntityType.JobSeeker);
    }

    [Theory]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-02")]
    [InlineData(PlatformEntityType.JobSeeker, "layla@example.org")]
    [InlineData(PlatformEntityType.Employer, "co-123")]
    [InlineData(PlatformEntityType.JobOffering, "ref-9")]
    public void Create_SupportsTheThreeEntityTypes(PlatformEntityType type, string expectedKey)
    {
        var core = type switch
        {
            PlatformEntityType.JobSeeker => new Dictionary<string, string> { ["fullName"] = "Layla", ["email"] = "Layla@Example.org" },
            PlatformEntityType.Employer => new Dictionary<string, string> { ["companyName"] = "Acme", ["companyId"] = "CO-123" },
            _ => new Dictionary<string, string> { ["title"] = "Engineer", ["postingReference"] = "REF-9" }
        };

        PlatformEntityRecord.Create(Guid.NewGuid(), type, core, Admin, false, At).IdentityKey.Should().Be(expectedKey);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-03")]
    public void Create_WhenKeyExists_ThrowsDuplicate()
    {
        var act = () => PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.JobSeeker, JobSeeker(), Admin, true, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.EntityDuplicate);
        ex.ExternalCode.Should().Be("E-AUM-DUPLICATE");
        ex.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-02")]
    [Trait("AC", "AC-04")]
    public void Create_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.JobSeeker, JobSeeker(), new Actor(Guid.NewGuid(), false), false, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.AdminOnly);
        ex.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Theory]
    [InlineData(PlatformEntityType.JobSeeker, "fullName")]
    [InlineData(PlatformEntityType.Employer, "companyId")]
    [InlineData(PlatformEntityType.JobOffering, "postingReference")]
    public void Create_WithoutRequiredField_ThrowsInvalidField(PlatformEntityType type, string presentField)
    {
        var act = () => PlatformEntityRecord.Create(Guid.NewGuid(), type, new Dictionary<string, string> { [presentField] = "x" }, Admin, false, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.EntityRequiredField);
        ex.ExternalCode.Should().Be("E-AUM-INVALID-FIELD");
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void Create_JobSeekerWithoutMobileOrEmail_ThrowsInvalidField()
    {
        var act = () => PlatformEntityRecord.Create(Guid.NewGuid(), PlatformEntityType.JobSeeker, new Dictionary<string, string> { ["fullName"] = "x" }, Admin, false, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.EntityRequiredField);
    }

    [Fact]
    public void EntityCore_DropsBlankValuesAndTrims()
    {
        var core = EntityCore.From(new Dictionary<string, string> { [" a "] = " v ", ["b"] = "  " });

        core.Fields.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("a", "v"));
    }
}
