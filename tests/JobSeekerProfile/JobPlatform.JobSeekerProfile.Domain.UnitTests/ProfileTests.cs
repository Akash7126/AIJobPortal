using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain.UnitTests;

public class ProfileTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Profile CreateActive() =>
        Profile.Create(Guid.NewGuid(), OwnerId, new FullName("Layla Haddad"), Email.Create("layla@example.org"), MobileNumber.Create("+970590000001"),
            Gender.Female, accountActive: true, OwnerId, At);

    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    [Trait("AC", "AC-01")]
    public void Create_WhenAccountNotActive_ThrowsAccountNotActive()
    {
        var act = () => Profile.Create(Guid.NewGuid(), OwnerId, new FullName("Layla"), Email.Create("layla@example.org"),
            MobileNumber.Create("+970590000001"), Gender.Female, accountActive: false, OwnerId, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.AccountNotActive);
        ex.ExternalCode.Should().Be(ErrorCodes.AccountNotActive);
        ex.Kind.Should().Be(BusinessRuleKind.BusinessRule);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    [Trait("AC", "AC-03")]
    public void Create_Success_RaisesCreatedEvent_AndLevel1IsComplete()
    {
        var profile = CreateActive();

        profile.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProfileCreatedDomainEvent>()
            .Which.OwnerAccountId.Should().Be(OwnerId);
        profile.SectionStatus[ProfileSections.Level1].Should().Be(SectionState.Complete);
        profile.CompletionPercent.Should().Be(CompletionWeights.Level1);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    [Trait("AC", "AC-02")]
    public void UpdateLevel1_ByNonOwner_ThrowsForbidden()
    {
        var profile = CreateActive();

        var act = () => profile.UpdateLevel1(new Actor(Guid.NewGuid()), new FullName("Someone"), profile.Email, profile.MobileNumber, Gender.Male,
            Guid.NewGuid(), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.NotOwner);
        ex.ExternalCode.Should().Be(ErrorCodes.Forbidden);
        ex.Kind.Should().Be(BusinessRuleKind.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    [Trait("AC", "AC-01")]
    public void UpdateEducation_ByOwner_AddsEntriesAndRecomputesCompletion()
    {
        var profile = CreateActive();

        profile.UpdateEducation(new Actor(OwnerId), new[] { ("BSc Computer Science", "Birzeit University", (DateTime?)null, (DateTime?)null) }, OwnerId, At);

        profile.Education.Should().ContainSingle().Which.Degree.Should().Be("BSc Computer Science");
        profile.SectionStatus[ProfileSections.Education].Should().Be(SectionState.Complete);
        profile.CompletionPercent.Should().Be(CompletionWeights.Level1 + CompletionWeights.Education);
        profile.DomainEvents.OfType<ProfileUpdatedDomainEvent>().Last().ChangedSections.Should().ContainSingle().Which.Should().Be(ProfileSections.Education);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    [Trait("AC", "AC-04")]
    public void ApplyExtractedData_NeverOverwritesUserSkills_AndSkipsDuplicates()
    {
        var profile = CreateActive();
        profile.UpdateSkills(new Actor(OwnerId), new[] { ("C#", SkillKind.Primary, SkillClass.Hard) }, OwnerId, At);

        profile.ApplyExtractedData(new[] { "C#", "SQL" }, new[] { "Backend Engineer" }, 5m, OwnerId, At);

        profile.Skills.Should().HaveCount(2);
        profile.Skills.Single(s => s.Name == "C#").Source.Should().Be(DataSource.User);
        profile.Skills.Single(s => s.Name == "SQL").Source.Should().Be(DataSource.ResumeParsing);
        profile.Experience.Should().ContainSingle().Which.Role.Should().Be("Backend Engineer");
        profile.YearsOfExperience.Should().Be(5m);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    [Trait("AC", "AC-02")]
    public void ApplyExtractedData_NeverOverwritesAnAlreadySetYearsOfExperience()
    {
        var profile = CreateActive();
        profile.UpdateSalaryExpectationAndAddress(new Actor(OwnerId), null, null, 10m, OwnerId, At);

        profile.ApplyExtractedData(Array.Empty<string>(), Array.Empty<string>(), 2m, OwnerId, At);

        profile.YearsOfExperience.Should().Be(10m, "a user-entered value must never be overwritten by parsed data");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    [Trait("AC", "AC-04")]
    public void ApplyReviewedSkills_ReplacesOnlyResumeParsingSourcedSkills()
    {
        var profile = CreateActive();
        profile.UpdateSkills(new Actor(OwnerId), new[] { ("C#", SkillKind.Primary, SkillClass.Hard) }, OwnerId, At);
        profile.ApplyExtractedData(new[] { "SQL" }, Array.Empty<string>(), null, OwnerId, At);

        profile.ApplyReviewedSkills(new[] { "Python" }, OwnerId, At);

        profile.Skills.Should().HaveCount(2);
        profile.Skills.Should().Contain(s => s.Name == "C#" && s.Source == DataSource.User);
        profile.Skills.Should().Contain(s => s.Name == "Python" && s.Source == DataSource.ResumeParsing);
        profile.Skills.Should().NotContain(s => s.Name == "SQL");
    }

    [Fact]
    public void Deactivate_IsIdempotent()
    {
        var profile = CreateActive();

        profile.Deactivate(OwnerId, At);
        var eventsAfterFirst = profile.DomainEvents.Count;
        profile.Deactivate(OwnerId, At);

        profile.Status.Should().Be(ProfileStatus.Deactivated);
        profile.DomainEvents.Should().HaveCount(eventsAfterFirst, "a second deactivation must not raise another event");
    }

    [Fact]
    public void ComputeCompletionRecommendation_ListsMissingSections_UntilComplete()
    {
        var profile = CreateActive();

        var (percent, missing) = profile.ComputeCompletionRecommendation();

        percent.Should().Be(CompletionWeights.Level1);
        missing.Should().Contain(new[] { ProfileSections.Education, ProfileSections.Experience, ProfileSections.Skills, ProfileSections.Training,
            ProfileSections.Level3 });
    }
}
