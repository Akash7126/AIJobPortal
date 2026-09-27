using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class HelpContentOrganizationTests
{
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    [Trait("AC", "AC-01")]
    public void AssignTopicAndRoles_SetsTopicAndRoles()
    {
        var organization = HelpContentOrganization.CreateFor(Guid.NewGuid());
        var topicId = Guid.NewGuid();

        organization.AssignTopicAndRoles(topicId, new[] { HelpRole.JobSeeker, HelpRole.Employer }, Admin);

        organization.TopicId.Should().Be(topicId);
        organization.Roles.Should().BeEquivalentTo(new[] { HelpRole.JobSeeker, HelpRole.Employer });
    }

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    [Trait("AC", "AC-02")]
    public void AssignTopicAndRoles_WithNullTopic_IsAllowed_FallsBackToUncategorizedAtReadTime()
    {
        var organization = HelpContentOrganization.CreateFor(Guid.NewGuid());

        organization.AssignTopicAndRoles(null, Array.Empty<HelpRole>(), Admin);

        organization.TopicId.Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    [Trait("AC", "AC-03")]
    public void AssignTopicAndRoles_Repeatedly_LaterSaveWins()
    {
        var organization = HelpContentOrganization.CreateFor(Guid.NewGuid());
        organization.AssignTopicAndRoles(Guid.NewGuid(), new[] { HelpRole.Guest }, Admin);

        var secondTopic = Guid.NewGuid();
        organization.AssignTopicAndRoles(secondTopic, new[] { HelpRole.Administrator }, Admin);

        organization.TopicId.Should().Be(secondTopic);
        organization.Roles.Should().ContainSingle().Which.Should().Be(HelpRole.Administrator);
    }

    [Fact]
    public void AssignTopicAndRoles_ByNonAdministrator_ThrowsForbidden()
    {
        var organization = HelpContentOrganization.CreateFor(Guid.NewGuid());

        var act = () => organization.AssignTopicAndRoles(null, Array.Empty<HelpRole>(), new Actor(Guid.NewGuid(), false));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.OrganizationAdminOnly);
        ex.ExternalCode.Should().Be("E-FAQHC-FORBIDDEN");
    }

    [Fact]
    public void HelpTopic_Remove_SoftRemoves()
    {
        var topic = HelpTopic.Create(Guid.NewGuid(), new LocalizedText(null, "Getting started"), Admin);

        topic.Remove(Admin);

        topic.IsRemoved.Should().BeTrue();
    }
}
