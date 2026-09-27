using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.UnitTests;

public class SystemSettingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static SystemSetting Setting(string key) => SystemSetting.Define(SystemSettingCatalog.Find(key)!, At);

    [Fact]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-01")]
    public void Change_WithValidValue_TakesEffectAndBumpsVersion()
    {
        var setting = Setting("upload.maxSizeMb");

        setting.Change("10", Admin, At.AddMinutes(1));

        setting.Value.Should().Be("10");
        setting.SettingVersion.Should().Be(2);
        setting.UpdatedBy.Should().Be(Admin.Id);
        setting.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SystemSettingChangedDomainEvent>().Which.SettingVersion.Should().Be(2);
    }

    [Theory]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-02")]
    [InlineData("upload.maxSizeMb", "0")]
    [InlineData("upload.maxSizeMb", "51")]
    [InlineData("platform.defaultLanguage", "fr")]
    public void Change_OutsideBounds_ThrowsOutOfRange(string key, string value)
    {
        var act = () => Setting(key).Change(value, Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.SettingOutOfRange);
        ex.ExternalCode.Should().Be("E-AUM-INVALID-FIELD");
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Theory]
    [InlineData("upload.maxSizeMb", "abc")]
    [InlineData("platform.maintenanceMode", "maybe")]
    [InlineData("platform.supportEmail", "  ")]
    public void Change_WithUnparsableValue_ThrowsInvalidValue(string key, string value)
    {
        var act = () => Setting(key).Change(value, Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.SettingInvalidValue);
    }

    [Fact]
    public void Change_StringLongerThanBound_ThrowsOutOfRange()
    {
        var act = () => Setting("platform.supportEmail").Change(new string('a', 255), Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.SettingOutOfRange);
    }

    [Theory]
    [InlineData(SettingValueType.Bool, "TRUE", "true")]
    [InlineData(SettingValueType.Int, " 7 ", "7")]
    [InlineData(SettingValueType.Decimal, "1.50", "1.50")]
    [InlineData(SettingValueType.Enum, "EN", "en")]
    public void Normalise_ProducesCanonicalText(SettingValueType type, string raw, string expected)
    {
        var bounds = type == SettingValueType.Enum ? SettingBounds.OneOf("ar", "en") : SettingBounds.None;

        SystemSetting.Normalise(type, raw, bounds).Should().Be(expected);
    }

    [Fact]
    public void Normalise_DecimalOutsideRange_Throws()
    {
        var act = () => SystemSetting.Normalise(SettingValueType.Decimal, "2.5", SettingBounds.Range(0, 1));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.SettingOutOfRange);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-03")]
    public void Change_TwiceInARow_LaterWinsAndEveryChangeIsLogged()
    {
        var setting = Setting("retention.months");
        var other = new Actor(Guid.NewGuid(), true);

        setting.Change("24", Admin, At);
        setting.Change("36", other, At.AddSeconds(1));

        setting.Value.Should().Be("36");
        setting.History.Should().HaveCount(2);
        setting.History.Select(h => (h.OldValue, h.NewValue, h.ChangedBy)).Should().Equal(("12", "24", Admin.Id), ("24", "36", other.Id));
    }

    [Fact]
    [Trait("Story", "US-3.1.4-06")]
    [Trait("AC", "AC-04")]
    public void Change_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => Setting("retention.months").Change("24", new Actor(Guid.NewGuid(), false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Fact]
    public void Catalog_EveryDefaultIsValid_AndKeysAreUnique()
    {
        SystemSettingCatalog.Definitions.Select(d => d.Key).Should().OnlyHaveUniqueItems();
        foreach (var definition in SystemSettingCatalog.Definitions)
        {
            SystemSetting.Define(definition, At).Value.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Define_RaisesNoEvent()
    {
        Setting("upload.maxSizeMb").DomainEvents.Should().BeEmpty();
    }
}
