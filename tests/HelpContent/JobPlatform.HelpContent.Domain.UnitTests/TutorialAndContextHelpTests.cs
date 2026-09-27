using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class TutorialProgressTests
{
    [Fact]
    [Trait("Story", "US-3.7.2-07")]
    [Trait("AC", "AC-01")]
    public void Complete_RecordsUserAndTutorialAndTimestamp()
    {
        var userId = Guid.NewGuid();
        var tutorialId = Guid.NewGuid();
        var now = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        var progress = TutorialProgress.Complete(Guid.NewGuid(), userId, tutorialId, now);

        progress.UserId.Should().Be(userId);
        progress.TutorialId.Should().Be(tutorialId);
        progress.CompletedAtUtc.Should().Be(now);
    }
}

public class ContextHelpMappingTests
{
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    [Trait("AC", "AC-01")]
    public void Create_MapsPageKeyToHelpContent()
    {
        var helpContentId = Guid.NewGuid();

        var mapping = ContextHelpMapping.Create(Guid.NewGuid(), "employer/dashboard", helpContentId, Admin);

        mapping.PageKey.Should().Be("employer/dashboard");
        mapping.HelpContentId.Should().Be(helpContentId);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    public void Repoint_ChangesTarget()
    {
        var mapping = ContextHelpMapping.Create(Guid.NewGuid(), "employer/dashboard", Guid.NewGuid(), Admin);
        var newTarget = Guid.NewGuid();

        mapping.Repoint(newTarget, Admin);

        mapping.HelpContentId.Should().Be(newTarget);
    }

    [Fact]
    public void Create_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => ContextHelpMapping.Create(Guid.NewGuid(), "x", Guid.NewGuid(), new Actor(Guid.NewGuid(), false));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ContextHelpMappingAdminOnly);
    }
}
