using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain.UnitTests;

public class ProfileShareLinkTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    [Trait("AC", "AC-02")]
    public void Generate_WhenSharingNotActivated_ThrowsSharingNotActivated()
    {
        var act = () => ProfileShareLink.Generate(Guid.NewGuid(), ProfileId, OwnerId, sharingActivated: false, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.SharingNotActivated);
        ex.ExternalCode.Should().Be(ErrorCodes.SharingNotActivated);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    [Trait("AC", "AC-01")]
    public void Generate_WhenActivated_CreatesAnActiveLinkWithAUrlSafeToken()
    {
        var link = ProfileShareLink.Generate(Guid.NewGuid(), ProfileId, OwnerId, sharingActivated: true, At);

        link.IsActive.Should().BeTrue();
        link.Token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-08")]
    [Trait("AC", "AC-04")]
    public void Deactivate_ByNonOwner_ThrowsForbidden()
    {
        var link = ProfileShareLink.Generate(Guid.NewGuid(), ProfileId, OwnerId, sharingActivated: true, At);

        var act = () => link.Deactivate(new Actor(Guid.NewGuid()));

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.Forbidden);
    }
}

public class JobPreferenceTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    [Trait("Story", "US-3.1.1-06")]
    [Trait("AC", "AC-02")]
    public void Set_ReplacesAllFieldsIndependently()
    {
        var preference = JobPreference.CreateEmpty(ProfileId);

        preference.Set(new[] { "FullTime" }, new[] { "IT" }, new[] { "Ramallah" }, new SalaryRange(1000, 2000, "USD"), new[] { WorkArrangement.Hybrid }, At);

        preference.JobTypes.Should().ContainSingle().Which.Should().Be("FullTime");
        preference.WorkArrangements.Should().ContainSingle().Which.Should().Be(WorkArrangement.Hybrid);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-06")]
    [Trait("AC", "AC-03")]
    public void Set_HasNoConcurrencyGuard_LastWriteWins()
    {
        var preference = JobPreference.CreateEmpty(ProfileId);

        preference.Set(new[] { "FullTime" }, Array.Empty<string>(), Array.Empty<string>(), null, Array.Empty<WorkArrangement>(), At);
        preference.Set(new[] { "PartTime" }, Array.Empty<string>(), Array.Empty<string>(), null, Array.Empty<WorkArrangement>(), At.AddMinutes(1));

        preference.JobTypes.Should().ContainSingle().Which.Should().Be("PartTime", "the later call always wins - no RowVersion check exists on this aggregate");
    }

    [Fact]
    public void EnsureOwnedBy_NonOwner_ThrowsForbidden()
    {
        var preference = JobPreference.CreateEmpty(ProfileId);

        var act = () => preference.EnsureOwnedBy(new Actor(Guid.NewGuid()), Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.Forbidden);
    }
}

public class PrivacySettingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    [Trait("Story", "US-3.1.1-07")]
    [Trait("AC", "AC-01")]
    public void SetVisibility_TakesEffectImmediately()
    {
        var setting = PrivacySetting.CreateDefault(ProfileId);

        setting.SetVisibility(@public: true, publicSharingActive: true);

        setting.Visibility.Should().Be(ProfileVisibility.Public);
        setting.PublicSharingActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.1-07")]
    [Trait("AC", "AC-03")]
    public void RequestDeletion_WhenAlreadyPending_ThrowsConflict()
    {
        var setting = PrivacySetting.CreateDefault(ProfileId);
        setting.RequestDeletion(At);

        var act = () => setting.RequestDeletion(At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.DeletionAlreadyPending);
        ex.ExternalCode.Should().Be(ErrorCodes.Conflict);
        ex.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-07")]
    [Trait("AC", "AC-04")]
    public void EnsureOwnedBy_NonOwner_ThrowsForbidden()
    {
        var setting = PrivacySetting.CreateDefault(ProfileId);

        var act = () => setting.EnsureOwnedBy(new Actor(Guid.NewGuid()), Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.Forbidden);
    }
}
